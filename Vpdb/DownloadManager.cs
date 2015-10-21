using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using VpdbAgent.Libs;

namespace VpdbAgent.Vpdb
{
	public interface IDownloadManager
	{
		IDownloadManager DownloadRelease(string id);
	}
	
	public class DownloadManager : IDownloadManager
	{
		// dependencies
		private readonly IVpdbClient _vpdbClient;
		private readonly Logger _logger;

		public DownloadManager(IVpdbClient vpdbClient, Logger logger)
		{
			_vpdbClient = vpdbClient;
			_logger = logger;
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

					var req = _vpdbClient.GetWebRequest(file.Reference.Url);
					_logger.Info("Downloading: {0}", req.RequestUri);

					// convert to observable
					var task = Task<WebResponse>.Factory.FromAsync(req.BeginGetResponse, req.EndGetResponse, req);
					Observable.FromAsync(ct => task).Subscribe(response => {
						_logger.Info("Got a stream!");
					});




					var jobQueue = new ParallelJobQueue(5);

					// subscribe to failures
					jobQueue.InnerQueue.WhenJobFails.Subscribe(e => Console.WriteLine("Job failed: {0}", e.Message));

					// subscribe to empty queue notification
					jobQueue.InnerQueue.WhenQueueEmpty.Subscribe(n => Console.WriteLine("Empty!"));

					int completed1 = 0, completed2 = 0, errors = 0;     // test counters
					foreach (var i in Enumerable.Range(0, 100)) {
						var x = i;
						jobQueue.Add(() => {

							Console.WriteLine("Thread {0}: {1}", Thread.CurrentThread.ManagedThreadId, x);

							if ((Interlocked.Increment(ref completed1) % 10) == 0) {
								// generate test exceptions
								throw new Exception("Text exception " + completed1);
							}

						}).Subscribe(n => Interlocked.Increment(ref completed2), e => Interlocked.Increment(ref errors));
					}

					jobQueue.InnerQueue.WhenQueueEmpty.Subscribe(_ =>
					{
						Console.WriteLine("DONE! Received 1: {0}, 2: {1}, errors: {2}", completed1, completed2, errors);
					});

				}, error => {
					_logger.Error(error, "Error retrieving release data.");
				});

			return this;
		}
	}
}
