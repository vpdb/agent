using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
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
				.Subscribe(release => {

					_logger.Info("Got something: {0}", release);
				}, error => {
					_logger.Error(error, "Error retrieving release data.");
				});

			return this;
		}
	}
}
