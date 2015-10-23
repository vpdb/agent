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

				}, error => {
					_logger.Error(error, "Error retrieving release data.");
				});

			return this;
		}
	}
}
