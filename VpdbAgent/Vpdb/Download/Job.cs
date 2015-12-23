using System;
using System.IO;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Models;
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
		// dependencies
		private static readonly IDatabaseManager DatabaseManager = Locator.Current.GetService<IDatabaseManager>();

		// persisted properties
		[DataMember] public string ReleaseId { get { return _releaseId; } set { this.RaiseAndSetIfChanged(ref _releaseId, value); } }
		[DataMember] public string FileId { get { return _fileId; } set { this.RaiseAndSetIfChanged(ref _fileId, value); } }
		[DataMember] public DateTime QueuedAt { get; set; } = DateTime.Now;
		[DataMember] public DateTime StartedAt { get; set; }
		[DataMember] public DateTime FinishedAt { get; set; }
		[DataMember] public long TransferredBytes { get; set; }
		[DataMember] public string ErrorMessage { get; set; }
		[DataMember] public VpdbImage Thumb { get; private set; }
		[DataMember] [JsonConverter(typeof(StringEnumConverter))] public JobStatus Status { get; set; } = JobStatus.Queued;
		[DataMember] [JsonConverter(typeof(StringEnumConverter))] public FileType FileType { get; set; }
		[DataMember] [JsonConverter(typeof(StringEnumConverter))] public VpdbTableFile.VpdbPlatform Platform { get; set; }

		// object lookups
		public VpdbRelease Release => _release.Value;
		public VpdbVersion Version => _version.Value;
		public VpdbTableFile TableFile => _tableFile.Value;
		public VpdbFile File => _file.Value;
		private ObservableAsPropertyHelper<VpdbRelease> _release;
		private ObservableAsPropertyHelper<VpdbVersion> _version;
		private ObservableAsPropertyHelper<VpdbTableFile> _tableFile;
		private ObservableAsPropertyHelper<VpdbFile> _file;

		// uri
		public Uri Uri => _uri.Value;
		private ObservableAsPropertyHelper<Uri> _uri;

		public readonly WebClient Client;

		// convenience props
		public string FilePath { get; set; }
		public bool IsFinished => Status != JobStatus.Transferring && Status != JobStatus.Queued;
		public TimeSpan DownloadTime => FinishedAt - StartedAt;
		public double DownloadBytesPerSecond => 1000d * TransferredBytes / DownloadTime.TotalMilliseconds;

		// streams
		public IObservable<JobStatus> WhenStatusChanges => _whenStatusChanges;
		public IObservable<DownloadProgressChangedEventArgs> WhenDownloadProgresses => _whenDownloadProgresses;

		// fields
		private readonly Subject<JobStatus> _whenStatusChanges = new Subject<JobStatus>();
		private readonly Subject<DownloadProgressChangedEventArgs> _whenDownloadProgresses = new Subject<DownloadProgressChangedEventArgs>();
		private CancellationToken _cancellationToken;
		private string _releaseId;
		private string _fileId;

		// dependencies
		private static readonly IVpdbClient VpdbClient = Locator.CurrentMutable.GetService<IVpdbClient>();
		private static readonly ILogger Logger = Locator.CurrentMutable.GetService<ILogger>();

		/// <summary>
		/// Constructor called when de-serializing
		/// </summary>
		public Job()
		{
			// setup output props only once database is ready, otherwise we end up with null values.
			DatabaseManager.Initialized
				.Where(isInitialized => isInitialized)
				.Subscribe(isInitialized => {

					this.WhenAnyValue(j => j.ReleaseId).Select(releaseId => DatabaseManager.GetRelease(ReleaseId)).ToProperty(this, j => j.Release, out _release);
					this.WhenAnyValue(j => j.FileId).Select(fileId => DatabaseManager.GetVersion(ReleaseId, fileId)).ToProperty(this, j => j.Version, out _version);
					this.WhenAnyValue(j => j.FileId).Select(fileId => DatabaseManager.GetTableFile(ReleaseId, fileId)).ToProperty(this, j => j.TableFile, out _tableFile);
					this.WhenAnyValue(j => j.FileId).Select(fileId => DatabaseManager.GetFile(fileId)).ToProperty(this, j => j.File, out _file);

					// uri
					this.WhenAnyValue(j => j.File).Where(f => f != null).Select(f => VpdbClient.GetUri(f.Url)).ToProperty(this, x => x.Uri, out _uri);
				});

			_whenStatusChanges.Subscribe(status => {
				Status = status;
			});
			Client = VpdbClient.GetWebClient();
		}

		/// <summary>
		/// Constructor called when creating a new job through the application for
		/// downloading a table file.
		/// </summary>
		/// <param name="release">Release to be downloaded</param>
		/// <param name="tableFile">File of the release to be downloaded</param>
		/// <param name="filetype">File type</param>
		/// <param name="platform">Platform the file belongs to</param>
		public Job(VpdbRelease release, VpdbTableFile tableFile, FileType filetype, VpdbTableFile.VpdbPlatform platform) : this()
		{
			// store file object in global db
			DatabaseManager.AddOrReplaceFile(tableFile.Reference);

			FileId = tableFile.Reference.Id;
			ReleaseId = release.Id;
			FileType = filetype;
			Thumb = tableFile.Thumb;
			Platform = platform;
			Logger.Info("Creating new release download job for {0} {1}.", filetype, Uri.AbsoluteUri);
		}

		/// <summary>
		/// Constructor called when creating a new job through the application for
		/// downloading a file not part of a release but related to a release.
		/// </summary>
		/// <param name="release">Release to be downloaded</param>
		/// <param name="file">File to be downloaded</param>
		/// <param name="filetype">File type</param>
		/// <param name="platform">Platform the file belongs to</param>
		public Job(VpdbRelease release, VpdbFile file, FileType filetype, VpdbTableFile.VpdbPlatform platform) : this()
		{
			// store file object in global db
			DatabaseManager.AddOrReplaceFile(file);

			FileId = file.Id;
			ReleaseId = release.Id;
			FileType = filetype;
			Platform = platform;
			Thumb = release.Thumb?.Image;
			Logger.Info("Creating new download job for {0} {1}.", filetype, Uri.AbsoluteUri);
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
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
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
				Logger.Warn("Transfer cannot be cancelled.");
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
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
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
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
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
		/// <param name="platform">Platform of the table file belonging to the download</param>
		/// <returns>Absolute path to local download location</returns>
		public string GetFileDestination(Platform platform = null)
		{
			if (platform == null) {
				Logger.Error("Platform not provided when it should have ({0})", FileType);
				return null;
			}

			switch (FileType) {
				case FileType.TableFile:
					return Path.Combine(platform.TablePath, File.Name);

				case FileType.TableScript:
					var scriptsFolder = Path.Combine(Path.GetDirectoryName(platform.TablePath), "Scripts");
					return Path.Combine(Directory.Exists(scriptsFolder) ? scriptsFolder : platform.TablePath, File.Name);

				case FileType.TableAuxiliary:
					
					return Path.Combine(platform.TablePath, File.Name);

				case FileType.TableMusic:
					var musicFolder = Path.Combine(Path.GetDirectoryName(platform.TablePath), "Music");
					if (!Directory.Exists(musicFolder)) {
						Directory.CreateDirectory(musicFolder);
					}
					return Path.Combine(musicFolder, File.Name);

				case FileType.TableImage:
					// todo handle desktop media (goes to "Table Images Desktop" folder)
					return Path.Combine(platform.MediaPath, MediaTableImages, Release.Game.DisplayName + Path.GetExtension(FilePath));

				case FileType.TableVideo:
					// todo handle desktop media (goes to "Table Videos Desktop" folder)
					return Path.Combine(platform.MediaPath, MediaTableVideos, Release.Game.DisplayName + Path.GetExtension(FilePath));

				case FileType.BackglassImage:
					return Path.Combine(platform.MediaPath, MediaBackglassImages, Release.Game.DisplayName + Path.GetExtension(FilePath));

				case FileType.WheelImage:
					return Path.Combine(platform.MediaPath, MediaWheelImages, Release.Game.DisplayName + Path.GetExtension(FilePath));

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
