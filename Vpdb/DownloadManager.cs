using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using ReactiveUI;
using VpdbAgent.Application;

namespace VpdbAgent.Vpdb
{

	/// <summary>
	/// Manages a download queue supporting parallel processing of downloads
	/// on multiple worker threads.
	/// </summary>
	public interface IDownloadManager
	{
		IDownloadManager DownloadRelease(string id);
		ReactiveList<DownloadJob> CurrentJobs { get; }
	}

	public class DownloadManager : IDownloadManager
	{
		// increase this on demand...
		public static readonly int MaximalSimultaneousDownloads = 2;

		// dependencies
		private readonly IVpdbClient _vpdbClient;
		private readonly IDatabaseManager _databaseManager;
		private readonly Logger _logger;

		// props
		public ReactiveList<DownloadJob> CurrentJobs { get; }

		// members
		private readonly Subject<DownloadJob> _jobs = new Subject<DownloadJob>();
		private readonly IDisposable _queue;
		private readonly string _downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpdbAgent", "Download");

		/// <summary>
		/// Constructor sets up queue and creates download folder if non-existing.
		/// </summary>
		public DownloadManager(IVpdbClient vpdbClient, IDatabaseManager databaseManager, Logger logger)
		{
			CurrentJobs = databaseManager.Database.DownloadJobs;

			_vpdbClient = vpdbClient;
			_databaseManager = databaseManager;
			_logger = logger;

			_queue = _jobs
				.ObserveOn(Scheduler.Default)
				.Select(job => Observable.DeferAsync(async token => Observable.Return(await ProcessDownload(job, token))))
				.Merge(MaximalSimultaneousDownloads)
				.Subscribe(job => {
					_databaseManager.Save();
					Console.WriteLine("Job {0} completed.", job);
				}, error => {
					_databaseManager.Save();
					Console.WriteLine("Error: {0}", error);
				});

			if (!Directory.Exists(_downloadPath)) {
				_logger.Info("Creating non-existing download folder at {0}.", _downloadPath);
				Directory.CreateDirectory(_downloadPath);
			}
		}

		public IDownloadManager DownloadRelease(string id)
		{
			// retrieve release details
			_logger.Info("Retrieving details for release {0}...", id);
			_vpdbClient.Api.GetRelease(id)
				.ObserveOn(Scheduler.Default)
				.Subscribe(release =>
				{
					// todo make this more sophisticated based on settings
					var file = release.Versions
						.SelectMany(v => v.Files)
						.FirstOrDefault(f => f.Flavor.Orientation.Equals("fs"));

					if (file == null) {
						_logger.Info("Release doesn't seem to have a FS release, aborting.");
						return;
					}

					// queue for download
					var job = new DownloadJob(release, file);
					AddToCurrentJobs(job);
					_jobs.OnNext(job);

				}, error => {
					_logger.Error(error, "Error retrieving release data.");
				});

			return this;
		}

		private async Task<DownloadJob> ProcessDownload(DownloadJob job, CancellationToken token)
		{
			var dest = Path.Combine(_downloadPath, job.Filename);

			_logger.Info("Starting downloading of {0} to {1}", job.Uri, dest);

			// setup cancelation
			token.Register(job.Client.CancelAsync);

			// update statuses
			job.OnStart();

			// do the grunt work
			try {
				await job.Client.DownloadFileTaskAsync(job.Uri, dest);
				job.OnSuccess();
				_logger.Info("Finished downloading of {0}", job.Uri);

			} catch (WebException e) {
				job.OnFailure(e);
				Console.WriteLine("Error downloading file (server error): {0}", e.Message);

			} catch (Exception e) {
				job.OnFailure(e);
				Console.WriteLine("Error downloading file: {0}", e.Message);
			}
			return job;
		}

		private void AddToCurrentJobs(DownloadJob job)
		{
			// update jobs back on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				_databaseManager.Database.DownloadJobs.Add(job);
				_databaseManager.Save();
			});
		}
	}
}
