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

namespace VpdbAgent.Vpdb
{
	public interface IDownloadManager
	{
		IDownloadManager DownloadRelease(string id);
		ReactiveList<DownloadJob> CurrentJobs { get; }
	}

	public class DownloadManager : IDownloadManager
	{
		// dependencies
		private readonly IVpdbClient _vpdbClient;
		private readonly Logger _logger;

		// props
		public ReactiveList<DownloadJob> CurrentJobs { get; } = new ReactiveList<DownloadJob>();

		// members
		private readonly Subject<DownloadJob> _jobs = new Subject<DownloadJob>();
		private readonly IDisposable _queue;
		private readonly string _downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpdbAgent", "Download");

		public DownloadManager(IVpdbClient vpdbClient, Logger logger)
		{
			_vpdbClient = vpdbClient;
			_logger = logger;

			_queue = _jobs
				.ObserveOn(Scheduler.Default)
				.Select(job => Observable.DeferAsync(async token => Observable.Return(await ProcessDownload(job, token))))
				.Merge(3)
				.Subscribe(job => {
					CurrentJobs.Remove(job);
					Console.WriteLine("Job {0} completed.", job);
				}, error => {
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
					_jobs.OnNext(new DownloadJob(release, file, _vpdbClient));

				}, error => {
					_logger.Error(error, "Error retrieving release data.");
				});

			return this;
		}

		private async Task<DownloadJob> ProcessDownload(DownloadJob job, CancellationToken token)
		{
			var dest = Path.Combine(_downloadPath, job.Filename);

			_logger.Info("Starting downloading of {0} to {1}", job.Uri, dest);
			AddToCurrentJobs(job);

			// setup cancelation
			token.Register(job.Client.CancelAsync);

			await Task.Delay(10000, token);
			//await job.Client.DownloadFileTaskAsync(job.Uri, dest);

			_logger.Info("Finished downloading of {0}", job.Uri);
			return job;
		}

		private void AddToCurrentJobs(DownloadJob job)
		{
			// update jobs back on main thread
			Application.Current.Dispatcher.Invoke(delegate {
				CurrentJobs.Add(job);
			});
		}
	}
}
