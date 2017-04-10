using System;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using LiteDB;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Models;
using ILogger = NLog.ILogger;

namespace VpdbAgent.Vpdb.Download
{
	/// <summary>
	/// A transfer from Vpdb to the local disk.
	/// 
	/// Represents one file to be downloaded. Lifecycle is the following:
	/// 
	///    * Queued - The job is sent to the queue and awaits a free slot.
	///    * Transferring - A download slot has been obtained and the file is downloading.
	///    * Completed - The transfer has finished successfully.
	///    * Failed - The transfer has failed.
	///    * Aborted - The transfer has been aborted.
	/// 
	/// Jobs are saved in the global database. In the future, the user might be
	/// able to clean up finished jobs, but currently they are kept indefinitely.
	/// </summary>
	public class Job : ReactiveObject, IComparable<Job>
	{
		// persisted properties
		[BsonId] public int Id { get; set; }
		[BsonRef(DatabaseManager.TableReleases)] public VpdbRelease Release { get { return _release; } set { this.RaiseAndSetIfChanged(ref _release, value); } }
		[BsonRef(DatabaseManager.TableFiles)] public VpdbFile File { get { return _file; } set { this.RaiseAndSetIfChanged(ref _file, value); } }
		public string FilePath { get; set; }
		public DateTime QueuedAt { get; set; } = DateTime.Now;
		public DateTime StartedAt { get; set; }
		public DateTime FinishedAt { get; set; }
		public long TransferredBytes { get; set; }
		public string ErrorMessage { get; set; }
		public JobStatus Status { get { return _status; } set { this.RaiseAndSetIfChanged(ref _status, value); } }
		public FileType FileType { get; set; }
		public VpdbTableFile.VpdbPlatform Platform { get; set; }
		public VpdbImage Thumb { get; private set; }

		// object lookups
		[BsonIgnore] public VpdbVersion Version => _version.Value;

		// unserialized props
		[BsonIgnore] public readonly WebClient Client;

		// convenience props
		[BsonIgnore] public bool IsFinished => Status != JobStatus.Transferring && Status != JobStatus.Queued;
		[BsonIgnore] public TimeSpan DownloadTime => FinishedAt - StartedAt;
		[BsonIgnore] public double DownloadBytesPerSecond => 1000d * TransferredBytes / DownloadTime.TotalMilliseconds;

		// streams
		[BsonIgnore] public IObservable<JobStatus> WhenStatusChanges => _whenStatusChanges;
		[BsonIgnore] public IObservable<DownloadProgressChangedEventArgs> WhenDownloadProgresses => _whenDownloadProgresses;

		// watched props
		private VpdbRelease _release;
		private VpdbFile _file;
		private JobStatus _status;
		private readonly ObservableAsPropertyHelper<VpdbVersion> _version;

		// fields
		private readonly Subject<JobStatus> _whenStatusChanges = new Subject<JobStatus>();
		private readonly Subject<DownloadProgressChangedEventArgs> _whenDownloadProgresses = new Subject<DownloadProgressChangedEventArgs>();
		private CancellationToken _cancellationToken;

		// dependencies
		private readonly ILogger _logger;

		private Job(IDependencyResolver resolver)
		{
			_logger = resolver.GetService<ILogger>();
			Client = resolver.GetService<IVpdbClient>().GetWebClient();
		}

		public Job() : this(Locator.Current) {

			// set Version
			this.WhenAnyValue(x => x.Release, x => x.File)
				.Where(x => x.Item1 != null && x.Item2 != null && x.Item1.Versions != null)
				.Select(x =>  x.Item1.GetVersion(x.Item2.Id))
				.ToProperty(this, j => j.Version, out _version);

			// update status
			_whenStatusChanges.Subscribe(status => {
				Status = status;
			});
		}

		/// <summary>
		/// Constructor called when de-serializing
		/// </summary>
		private Job(VpdbRelease release, VpdbFile file) : this()
		{
			Release = release;
			File = file;
		}

		/// <summary>
		/// Constructor called when creating a new job through the application for
		/// downloading a table file.
		/// </summary>
		/// <param name="release">Release to be downloaded</param>
		/// <param name="tableFile">File of the release to be downloaded</param>
		/// <param name="filetype">Where does this end up?</param>
		/// <param name="platform">Platform the file belongs to</param>
		public Job(VpdbRelease release, VpdbTableFile tableFile, FileType filetype, VpdbTableFile.VpdbPlatform platform) : this(release, tableFile.Reference)
		{
			FileType = filetype;
			Thumb = tableFile.Thumb;
			Platform = platform;
			_logger.Info("Creating new release download job for {0} {1}.", filetype, File.Uri.AbsoluteUri);
		}

		/// <summary>
		/// Constructor called when creating a new job through the application for
		/// downloading a file not part of a release but related to a release, 
		/// like playfield image or backglass.
		/// </summary>
		/// <param name="release">Release to be downloaded</param>
		/// <param name="file">File to be downloaded</param>
		/// <param name="filetype">Where does this end up?</param>
		/// <param name="platform">Platform the file belongs to</param>
		public Job(VpdbRelease release, VpdbFile file, FileType filetype, VpdbTableFile.VpdbPlatform platform) : this(release, file)
		{
			FileType = filetype;
			Platform = platform;
			Thumb = release.Thumb?.Image;
			_logger.Info("Creating new download job for {0} {1}.", filetype, File.Uri.AbsoluteUri);
		}

		/// <summary>
		/// Transfer has started
		/// </summary>
		public void OnStart(CancellationToken token, string filePath)
		{
			if (Status == JobStatus.Completed || Status == JobStatus.Transferring) {
				throw new InvalidOperationException("Cannot start a job that is " + Status + ".");
			}

			_cancellationToken = token;
			FilePath = filePath;

			StartedAt = DateTime.Now;
			Client.DownloadProgressChanged += OnProgressChanged;

			// on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(() => {
				_whenStatusChanges.OnNext(JobStatus.Transferring);
			});

			// update transfer size
			WhenDownloadProgresses.Subscribe(progress => { TransferredBytes = progress.BytesReceived; });
		}

		/// <summary>
		/// Sets the status to <see cref="JobStatus.Queued"/> and resets the timestamp.
		/// </summary>
		public void Initialize()
		{
			Status = JobStatus.Queued;
			QueuedAt = DateTime.Now;
		}

		/// <summary>
		/// Cancels a job.
		/// </summary>
		public void Cancel()
		{
			if (_cancellationToken.CanBeCanceled) {
				Client.CancelAsync();

			} else {
				_logger.Warn("Transfer cannot be cancelled.");
			}
		}

		/// <summary>
		/// Transfer has finished (succeeded or failed)
		/// </summary>
		public void OnFinished()
		{
			FinishedAt = DateTime.Now;
		}

		/// <summary>
		/// Transfer has succeeded
		/// </summary>
		public void OnSuccess()
		{
			// on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(() => {
				OnFinished();
				_whenStatusChanges.OnNext(JobStatus.Completed);
			});
			Client.DownloadProgressChanged -= OnProgressChanged;
		}

		/// <summary>
		/// Transfer has been cancelled
		/// </summary>
		public void OnCancelled()
		{
			System.Windows.Application.Current.Dispatcher.Invoke(() => {
				OnFinished();
				_whenStatusChanges.OnNext(JobStatus.Aborted);
			});
			Client.DownloadProgressChanged -= OnProgressChanged;
		}

		/// <summary>
		/// Transfer has failed
		/// </summary>
		/// <param name="e"></param>
		public void OnFailure(Exception e)
		{
			System.Windows.Application.Current.Dispatcher.Invoke(delegate
			{
				ErrorMessage = e.Message;
				OnFinished();
				_whenStatusChanges.OnNext(JobStatus.Failed);
			});
			Client.DownloadProgressChanged -= OnProgressChanged;
		}

		/// <summary>
		/// Returns the absolute path of where the file should moved to after
		/// downloading.
		/// </summary>
		/// <param name="system">System of the table file belonging to the download</param>
		/// <returns>Absolute path to local download location</returns>
		public string GetFileDestination(PinballXSystem system = null)
		{
			if (system == null) {
				_logger.Error("Platform not provided when it should have ({0})", FileType);
				return null;
			}

			switch (FileType) {
				case FileType.TableFile:
					return Path.Combine(system.TablePath, File.Name);

				case FileType.TableScript:
					var scriptsFolder = Path.Combine(Path.GetDirectoryName(system.TablePath), "Scripts");
					return Path.Combine(Directory.Exists(scriptsFolder) ? scriptsFolder : system.TablePath, File.Name);

				case FileType.TableAuxiliary:
					
					return Path.Combine(system.TablePath, File.Name);

				case FileType.TableMusic:
					var musicFolder = Path.Combine(Path.GetDirectoryName(system.TablePath), "Music");
					if (!Directory.Exists(musicFolder)) {
						Directory.CreateDirectory(musicFolder);
					}
					return Path.Combine(musicFolder, File.Name);

				case FileType.TableImage:
					// todo handle desktop media (goes to "Table Images Desktop" folder)
					return Path.Combine(system.MediaPath, MediaTableImages, Release.Game.DisplayName + Path.GetExtension(FilePath));

				case FileType.TableVideo:
					// todo handle desktop media (goes to "Table Videos Desktop" folder)
					return Path.Combine(system.MediaPath, MediaTableVideos, Release.Game.DisplayName + Path.GetExtension(FilePath));

				case FileType.BackglassImage:
					return Path.Combine(system.MediaPath, MediaBackglassImages, Release.Game.DisplayName + Path.GetExtension(FilePath));

				case FileType.WheelImage:
					return Path.Combine(system.MediaPath, MediaWheelImages, Release.Game.DisplayName + Path.GetExtension(FilePath));

				case FileType.Rom: // todo
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		/// <summary>
		/// Transfer progress has changed
		/// </summary>
		private void OnProgressChanged(object sender, DownloadProgressChangedEventArgs p)
		{
			_whenDownloadProgresses.OnNext(p);
		}

		/// <summary>
		/// Sorts first by status, then by queued at.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public int CompareTo(Job other)
		{
			var byStatus = Array.IndexOf(StatusOrder, Status) - Array.IndexOf(StatusOrder, other.Status);
			return byStatus != 0 ? byStatus : ((other.QueuedAt - QueuedAt).TotalMilliseconds > 0 ? 1 : -1);
		}

		/// <summary>
		/// Defines the order in which statuses are sorted by.
		/// </summary>
		private static readonly JobStatus[] StatusOrder =
		{
			JobStatus.Transferring, JobStatus.Queued, JobStatus.Aborted, JobStatus.Failed, JobStatus.Completed
		};

		/// <summary>
		/// Statuses of a download job
		/// </summary>
		public enum JobStatus
		{
			/// <summary>
			/// Job is about to be transferred but still waiting for a slot to be freed
			/// </summary>
			Queued,

			/// <summary>
			/// Job is currently transferring
			/// </summary>
			Transferring,

			/// <summary>
			/// Job has successfully terminated
			/// </summary>
			Completed,

			/// <summary>
			/// Job terminated but failed
			/// </summary>
			Failed,

			/// <summary>
			/// Job was aborted
			/// </summary>
			Aborted
		}

		// media folder names
		public const string MediaBackglassImages = "Backglass Images";
		public const string MediaBackglassVideos = "Backglass Videos";
		public const string MediaDmdImages = "DMD Images";
		public const string MediaDmdVideos = "DMD Videos";
		public const string MediaRealDmdImages = "Real DMD Images";
		public const string MediaRealDmdVideos = "Real DMD Videos";
		public const string MediaTableAudio = "Table Audio";
		public const string MediaTableImages = "Table Images";
		public const string MediaTableImagesDesktop = "Table Images Desktop";
		public const string MediaTableVideos = "Table Videos";
		public const string MediaTableVideosDesktop = "Table Videos Desktop";
		public const string MediaWheelImages = "Wheel Images";
	}
}
