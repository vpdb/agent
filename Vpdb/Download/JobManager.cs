using System;
using System.IO;
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
		IObservable<Job.JobStatus> WhenStatusChanged { get; }

		/// <summary>
		/// An observable that produces a value when a job finished downloading
		/// successfully.
		/// </summary>
		IObservable<Job> WhenDownloaded { get; }

		/// <summary>
		/// A list of all current, future and past jobs (i.e. independent of their 
		/// status)
		/// </summary>
		ReactiveList<Job> CurrentJobs { get; }
	}

	/// <summary>
	/// Application logic for <see cref="IJobManager"/>.
	/// </summary>
	public class JobManager : IJobManager
	{
		// increase this on demand...
		public static readonly int MaximalSimultaneousDownloads = 2;

		// dependencies
		private readonly IDatabaseManager _databaseManager;
		private readonly IMessageManager _messageManager;
		private readonly CrashManager _crashManager;
		private readonly Logger _logger;

		// props
		public ReactiveList<Job> CurrentJobs { get; }
		public IObservable<Job.JobStatus> WhenStatusChanged => _whenStatusChanged;
		public IObservable<Job> WhenDownloaded => _whenDownloaded;

		// members
		private readonly Subject<Job> _jobs = new Subject<Job>();
		private readonly Subject<Job.JobStatus> _whenStatusChanged = new Subject<Job.JobStatus>();
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
		public JobManager(IDatabaseManager databaseManager, IMessageManager messageManager, CrashManager crashManager, Logger logger)
		{
			CurrentJobs = databaseManager.GetJobs();

			_databaseManager = databaseManager;
			_messageManager = messageManager;
			_crashManager = crashManager;
			_logger = logger;

			// setup transfer queue
			_queue = _jobs
				.ObserveOn(Scheduler.Default)
				.Select(job => Observable.DeferAsync(async token => Observable.Return(await ProcessDownload(job, token))))
				.Merge(MaximalSimultaneousDownloads)
				.Subscribe(job => {
					_databaseManager.Save();

					if (job.Status != Job.JobStatus.Aborted) {
						_whenDownloaded.OnNext(job);
					}
				}, error => {
					_databaseManager.Save();
					// todo treat error in ui
					_logger.Error(error, "Error: {0}", error.Message);
				});

			if (!Directory.Exists(_downloadPath)) {
				_logger.Info("Creating non-existing download folder at {0}.", _downloadPath);
				Directory.CreateDirectory(_downloadPath);
			}
		}

		public IJobManager AddJob(Job job)
		{
			AddToCurrentJobs(job);
			_jobs.OnNext(job);
			return this;
		}

		public IJobManager RetryJob(Job job)
		{
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				job.Initialize();
				job.WhenStatusChanges.Subscribe(status => _whenStatusChanged.OnNext(status));
				_databaseManager.Save();
				_jobs.OnNext(job);
			});
			return this;
		}
	
		public IJobManager DeleteJob(Job job)
		{
			// update jobs back on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				_databaseManager.RemoveJob(job);
				_databaseManager.Save();
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

			_logger.Info("Starting downloading of {0} to {1}", job.Uri, dest);

			// setup cancelation
			token.Register(job.Client.CancelAsync);

			// update statuses
			job.OnStart(token, dest);

			// do the grunt work
			try {
				await job.Client.DownloadFileTaskAsync(job.Uri, dest);
				job.OnSuccess();
				_logger.Info("Finished downloading of {0}", job.Uri);
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
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				job.WhenStatusChanges.Subscribe(status => _whenStatusChanged.OnNext(status));
				_databaseManager.AddJob(job);
				_databaseManager.Save();
			});
		}

	}
}
