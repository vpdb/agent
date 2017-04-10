using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using ReactiveUI;
using VpdbAgent.Application;

namespace VpdbAgent.Vpdb.Download
{
	/// <summary>
	/// Manages a download queue supporting parallel processing of downloads
	/// on multiple worker threads.
	/// </summary>
	public interface IJobManager
	{

		/// <summary>
		/// Initializes the job manager.
		/// </summary>
		/// <returns>This instance</returns>
		IJobManager Initialize();

		/// <summary>
		/// A list of all current, future and past jobs (i.e. independent of their 
		/// status)
		/// </summary>
		ReactiveList<Job> CurrentJobs { get; }

		/// <summary>
		/// Adds a new job to be processed.
		/// </summary>
		/// <param name="job">Job to add</param>
		/// <returns>This instance</returns>
		IJobManager AddJob(Job job);

		/// <summary>
		/// Deletes a job from the database.
		/// </summary>
		/// <param name="job"></param>
		/// <returns>This instance</returns>
		IJobManager DeleteJob(Job job);

		/// <summary>
		/// Retries a failed or aborted job.
		/// </summary>
		/// <param name="job"></param>
		/// <returns>This instance</returns>
		IJobManager RetryJob(Job job);

		/// <summary>
		/// An observable that produces values as when any job in the queue
		/// (active or not) changes status.
		/// </summary>
		IObservable<Job> WhenStatusChanged { get; }

		/// <summary>
		/// An observable that produces a value when a job finished downloading
		/// successfully.
		/// </summary>
		IObservable<Job> WhenDownloaded { get; }
	}

	/// <summary>
	/// Application logic for <see cref="IJobManager"/>.
	/// </summary>
	public class JobManager : IJobManager
	{
		// increase this on demand...
		public const int MaximalSimultaneousDownloads = 2;

		// dependencies
		private readonly IDatabaseManager _databaseManager;
		private readonly IMessageManager _messageManager;
		private readonly ILogger _logger;
		private readonly CrashManager _crashManager;

		// props
		public ReactiveList<Job> CurrentJobs { get; } = new ReactiveList<Job>();
		public IObservable<Job> WhenStatusChanged => _whenStatusChanged;
		public IObservable<Job> WhenDownloaded => _whenDownloaded;

		// members
		private readonly Subject<Job> _jobs = new Subject<Job>();
		private readonly Subject<Job> _whenStatusChanged = new Subject<Job>();
		private readonly Subject<Job> _whenDownloaded = new Subject<Job>();
		private readonly IDisposable _queue;
		private readonly string _downloadPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
			SettingsManager.DataFolder,
			"Download"
		);

		/// <summary>
		/// Constructor sets up queue and creates download folder if non-existing.
		/// </summary>
		public JobManager(IDatabaseManager databaseManager, IMessageManager messageManager, ILogger logger, CrashManager crashManager)
		{
			_databaseManager = databaseManager;
			_messageManager = messageManager;
			_logger = logger;
			_crashManager = crashManager;

			// setup transfer queue
			_queue = _jobs
				.ObserveOn(Scheduler.Default)
				.Select(job => Observable.DeferAsync(async token => Observable.Return(await ProcessDownload(job, token))))
				.Merge(MaximalSimultaneousDownloads)
				.Subscribe(job => {
					_databaseManager.SaveJob(job);

					if (job.Status != Job.JobStatus.Aborted) {
						_whenDownloaded.OnNext(job);
					}
				}, error => {
					// todo treat error in ui
					_logger.Error(error, "Error: {0}", error.Message);
				});

			// save job when status changes
			_whenStatusChanged.Sample(TimeSpan.FromMilliseconds(200)).Subscribe(_databaseManager.SaveJob);

			if (!Directory.Exists(_downloadPath)) {
				_logger.Info("Creating non-existing download folder at {0}.", _downloadPath);
				Directory.CreateDirectory(_downloadPath);
			}
		}

		public IJobManager Initialize()
		{
			var jobs = _databaseManager.GetJobs();
			using (CurrentJobs.SuppressChangeNotifications()) {
				CurrentJobs.AddRange(jobs);
			}

			// add queued to queue
			jobs.Where(j => j.Status == Job.JobStatus.Queued).ToList().ForEach(job => {
				job.WhenStatusChanges.Subscribe(status => _whenStatusChanged.OnNext(job));
				_jobs.OnNext(job);
			});
			return this;	
		}

		public IJobManager AddJob(Job job)
		{
			// persist to db and memory
			_databaseManager.AddJob(job);
			CurrentJobs.Add(job);

			// queue it up so it gets downloaded
			AddToCurrentJobs(job);
			_jobs.OnNext(job);
			return this;
		}

		public IJobManager RetryJob(Job job)
		{
			System.Windows.Application.Current.Dispatcher.Invoke(() => {
				job.Initialize();
				job.WhenStatusChanges.Subscribe(status => _whenStatusChanged.OnNext(job));
				_jobs.OnNext(job);
				_databaseManager.SaveJob(job);
			});
			return this;
		}
	
		public IJobManager DeleteJob(Job job)
		{
			_databaseManager.RemoveJob(job);

			// update jobs back on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(() => {
				CurrentJobs.Remove(job);
			});
			return this;
		}

		public void CancelAll()
		{
			_queue.Dispose();
		}

		/// <summary>
		/// Starts the download.
		/// </summary>
		/// <remarks>
		/// This is called when the queue has a new download slot available.
		/// </remarks>
		/// <param name="job">Job to start</param>
		/// <param name="token">Cancelation token</param>
		/// <returns>Job</returns>
		private async Task<Job> ProcessDownload(Job job, CancellationToken token)
		{
			var dest = Path.Combine(_downloadPath, job.File.Name);

			_logger.Info("Starting downloading of {0} to {1}", job.File.Uri, dest);

			// setup cancelation
			token.Register(job.Client.CancelAsync);

			// update statuses
			job.OnStart(token, dest);

			// do the grunt work
			try {
				await job.Client.DownloadFileTaskAsync(job.File.Uri, dest);
				job.OnSuccess();
				_logger.Info("Finished downloading of {0}", job.File.Uri);

			} catch (WebException e) {
				if (e.Status == WebExceptionStatus.RequestCanceled) {
					job.OnCancelled();
				} else {
					job.OnFailure(e);
					_logger.Error(e, "Error downloading file (server error): {0}", e.Message);
					_crashManager.Report(e, "network");
					_messageManager.LogError(e, "Error downloading file");
				}

			} catch (Exception e) {
				job.OnFailure(e);
				_logger.Error(e, "Error downloading file: {0}", e.Message);
				_crashManager.Report(e, "network");
				_messageManager.LogError(e, "Error downloading file");
			}
			return job;
		}

		/// <summary>
		/// Persists a new job and forwards its changing status to the
		/// global one.
		/// </summary>
		/// <param name="job">Job to add</param>
		private void AddToCurrentJobs(Job job)
		{
			// update jobs back on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(() => job.WhenStatusChanges.Subscribe(status => _whenStatusChanged.OnNext(job)));
		}
	}
}
