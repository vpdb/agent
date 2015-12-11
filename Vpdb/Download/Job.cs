using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Models;

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
		[DataMember]
		public VpdbRelease Release {
			get { return _release; }
			set {
				_release = value;
				if (_tableFile != null) {
					Version = _release.Versions.FirstOrDefault(version => version.Files.Contains(_tableFile));
				}
		} }
		[DataMember]
		public VpdbTableFile TableFile {
			get { return _tableFile; }
			set {
				_tableFile = value;
				File = value.Reference;
				if (_release != null) {
					Version = _release.Versions.FirstOrDefault(version => version.Files.Contains(_tableFile));
				}
		} }
		[DataMember]
		public VpdbFile File
		{
			get { return _file; }
			set {
				_file = value;
				Uri = VpdbClient.GetUri(value.Url);
				FileName = value.Name;
			}
		}
		[DataMember] public DateTime QueuedAt { get; set; } = DateTime.Now;
		[DataMember] public DateTime StartedAt { get; set; }
		[DataMember] public DateTime FinishedAt { get; set; }
		[DataMember] public long TransferredBytes { get; set; }
		[DataMember] public string ErrorMessage { get; set; }
		[DataMember] public VpdbImage Thumb { get; private set; }
		[DataMember] [JsonConverter(typeof(StringEnumConverter))] public JobStatus Status { get; set; } = JobStatus.Queued;
		[DataMember] [JsonConverter(typeof(StringEnumConverter))] public FileType FileType { get; set; }
		[DataMember] [JsonConverter(typeof(StringEnumConverter))] public VpdbTableFile.VpdbPlatform Platform { get; set; }

		// business props
		public Uri Uri { get; private set; }
		public readonly WebClient Client;

		// convenience props
		public string FilePath { get; set; }
		public string FileName { get; private set; }
		public VpdbVersion Version { get; private set; }
		public bool IsFinished => Status != JobStatus.Transferring && Status != JobStatus.Queued;
		public TimeSpan DownloadTime => FinishedAt - StartedAt;
		public double DownloadBytesPerSecond => 1000d * TransferredBytes / DownloadTime.TotalMilliseconds;

		// streams
		public IObservable<JobStatus> WhenStatusChanges => _whenStatusChanges;
		public IObservable<DownloadProgressChangedEventArgs> WhenDownloadProgresses => _whenDownloadProgresses;

		// fields
		private VpdbTableFile _tableFile;
		private VpdbFile _file;
		private VpdbRelease _release;
		private readonly Subject<JobStatus> _whenStatusChanges = new Subject<JobStatus>();
		private readonly Subject<DownloadProgressChangedEventArgs> _whenDownloadProgresses = new Subject<DownloadProgressChangedEventArgs>();
		private CancellationToken _cancellationToken;

		// dependencies
		private static readonly IVpdbClient VpdbClient = Locator.CurrentMutable.GetService<IVpdbClient>();
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();

		/// <summary>
		/// Constructor called when de-serializing
		/// </summary>
		public Job()
		{
			_whenStatusChanges.Subscribe(status => {
				Status = status;
			});
			Client = VpdbClient.GetWebClient();
		}

		/// <summary>
		/// Constructor called when creating new job through the application
		/// </summary>
		/// <param name="release">Release to be downloaded</param>
		/// <param name="tableFile">File of the release to be downloaded</param>
		/// <param name="filetype">File type</param>
		/// <param name="platform">Platform the file belongs to</param>
		public Job(VpdbRelease release, VpdbTableFile tableFile, FileType filetype, VpdbTableFile.VpdbPlatform platform) : this()
		{
			TableFile = tableFile;
			Release = release;
			FileType = filetype;
			Thumb = tableFile.Thumb;
			Platform = platform;
			Logger.Info("Creating new download job for {0} {1}.", filetype, Uri.AbsoluteUri);
		}

		/// <summary>
		/// Constructor called when creating new job through the application
		/// </summary>
		/// <param name="release">Release to be downloaded</param>
		/// <param name="file">File to be downloaded</param>
		/// <param name="filetype">File type</param>
		/// <param name="platform">Platform the file belongs to</param>
		public Job(VpdbRelease release, VpdbFile file, FileType filetype, VpdbTableFile.VpdbPlatform platform) : this()
		{
			Release = release;
			File = file;
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

		public void Initialize()
		{
			Status = JobStatus.Queued;
			QueuedAt = DateTime.Now;
		}

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
		public static readonly string MediaBackglassImages = "Backglass Images";
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
