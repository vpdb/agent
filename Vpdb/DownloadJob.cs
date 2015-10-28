using System;
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
using VpdbAgent.Vpdb.Models;
using File = VpdbAgent.Vpdb.Models.File;
using Version = VpdbAgent.Vpdb.Models.Version;

namespace VpdbAgent.Vpdb
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
	public class DownloadJob : ReactiveObject, IComparable<DownloadJob>
	{
		// persisted properties
		[DataMember]
		public Release Release {
			get { return _release; }
			set {
				_release = value;
				if (_file != null) {
					Version = _release.Versions.FirstOrDefault(version => version.Files.Contains(_file));
				}
		} }
		[DataMember]
		public File File {
			get { return _file; }
			set {
				_file = value;
				Uri = VpdbClient.GetUri(value.Reference.Url);
				FileName = value.Reference.Name;
				if (_release != null) {
					Version = _release.Versions.FirstOrDefault(version => version.Files.Contains(_file));
				}
		} }

		[DataMember] [JsonConverter(typeof(StringEnumConverter))]
		public JobStatus Status { get; private set; } = JobStatus.Queued;
		[DataMember] public DateTime QueuedAt { get; } = DateTime.Now;
		[DataMember] public DateTime StartedAt { get; private set; }
		[DataMember] public DateTime FinishedAt { get; private set; }
		[DataMember] public long TransferredBytes { get; private set; }
		[DataMember] public string ErrorMessage { get; private set; }

		// business props
		public Uri Uri { get; private set; }
		public readonly WebClient Client;

		// convenience props
		public string FilePath { get; set; }
		public string FileName { get; private set; }
		public Version Version { get; private set; }
		public bool IsFinished => Status != JobStatus.Transferring && Status != JobStatus.Queued;
		public TimeSpan DownloadTime => FinishedAt - StartedAt;
		public double DownloadBytesPerSecond => 1000d * TransferredBytes / DownloadTime.TotalMilliseconds;

		// streams
		public IObservable<JobStatus> WhenStatusChanges => _whenStatusChanges;
		public IObservable<DownloadProgressChangedEventArgs> WhenDownloadProgresses => _whenDownloadProgresses;

		// fields
		private File _file;
		private Release _release;
		private readonly Subject<JobStatus> _whenStatusChanges = new Subject<JobStatus>();
		private readonly Subject<DownloadProgressChangedEventArgs> _whenDownloadProgresses = new Subject<DownloadProgressChangedEventArgs>();
		private CancellationToken _cancellationToken;

		// dependencies
		private static readonly IVpdbClient VpdbClient = Locator.CurrentMutable.GetService<IVpdbClient>();
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();

		/// <summary>
		/// Constructor called when de-serializing
		/// </summary>
		public DownloadJob()
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
		/// <param name="file">File of the release to be downloaded</param>
		public DownloadJob(Release release, File file) : this()
		{
			File = file;
			Release = release;
			Logger.Info("Creating new download job for {0}.", Uri.AbsoluteUri);
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
			WhenDownloadProgresses
				.Subscribe(progress => {
					TransferredBytes = progress.BytesReceived;
					// on main thread
					//System.Windows.Application.Current.Dispatcher.Invoke(delegate {
					//	TransferredBytes = progress.BytesReceived;
					//});
				});
		}

		public void Cancel()
		{
			if (_cancellationToken != null && _cancellationToken.CanBeCanceled) {
				Client.CancelAsync();

			} else {
				Logger.Info("Transfer cannot be cancelled.");
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
		public int CompareTo(DownloadJob other)
		{
			var byStatus = Array.IndexOf(StatusOrder, this.Status) - Array.IndexOf(StatusOrder, other.Status);
			return byStatus != 0 ? byStatus : ((other.QueuedAt - QueuedAt).TotalMilliseconds > 0 ? 1 : -1);
		}

		/// <summary>
		/// Defines the order in which statuses are sorted by.
		/// </summary>
		private static readonly JobStatus[] StatusOrder = {
			JobStatus.Transferring,
			JobStatus.Queued,
			JobStatus.Aborted,
			JobStatus.Failed,
			JobStatus.Completed
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
	}
}
