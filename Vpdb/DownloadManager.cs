using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using ReactiveUI;
using VpdbAgent.Application;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Vpdb
{
	public interface IDownloadManager
	{
		/// <summary>
		/// Downloads or upgrades a release, including table image, ROMs and other 
		/// media, based on the user's settings.
		/// </summary>
		/// <remarks>
		/// File selection is based on the user's preferences. This is typically
		/// called when the user stars a release, where we have no more data
		/// than the release. 
		/// </remarks>
		/// <param name="releaseId">ID of the release</param>
		/// <returns>This instance</returns>
		IDownloadManager DownloadRelease(string releaseId);
	}
	
	public class DownloadManager : IDownloadManager
	{
		// dependencies
		private readonly IJobManager _jobManager;
		private readonly IVpdbClient _vpdbClient;
		private readonly ISettingsManager _settingsManager;
		private readonly Logger _logger;

		private readonly List<IFlavorMatcher> _flavorMatchers = new List<IFlavorMatcher>();

		public DownloadManager(IJobManager jobManager, IVpdbClient vpdbClient, ISettingsManager settingsManager, Logger logger)
		{
			_jobManager = jobManager;
			_vpdbClient = vpdbClient;
			_settingsManager = settingsManager;
			_logger = logger;

			// setup flavor matchers as soon as settings are available.
			_settingsManager.Settings.WhenAny(
				s => s.DownloadOrientation,
				s => s.DownloadOrientationFallback,
				s => s.DownloadLighting,
				s => s.DownloadLightingFallback,
				(a, b, c, d) => Unit.Default).Subscribe(_ => {
					_logger.Info("Setting up flavor matchers.");
					_flavorMatchers.Clear();
					_flavorMatchers.Add(new OrientationMatcher(_settingsManager.Settings));
					_flavorMatchers.Add(new LightingMatcher(_settingsManager.Settings));
				});
		}

		public IDownloadManager DownloadRelease(string id)
		{
			// retrieve release details
			_logger.Info("Retrieving details for release {0}...", id);
			_vpdbClient.Api.GetRelease(id)
				.ObserveOn(Scheduler.Default)
				.Subscribe(release => {

					// match file based on settings
					var file = release.Versions
						.SelectMany(v => v.Files)
						.Where(FlavorMatches)
						.Select(f => new { f, weight = FlavorWeight(f) })
						.OrderBy(x => x.weight)
						.Select(x => x.f)
						.LastOrDefault();

					// check if match
					if (file == null) {
						_logger.Info("Nothing matched current flavor configuration, skipping.");
						return;
					}

					// download
					_jobManager.DownloadRelease(release, file);

				}, exception => _vpdbClient.HandleApiError(exception, "retrieving release details during download"));

			return this;
		}

		/// <summary>
		/// Checks if the flavor of the file is acceptable by the user's flavor settings.
		/// </summary>
		/// <param name="file">File to check</param>
		/// <returns>Returns true if the primary or secondary flavor setting of ALL flavors matches, false otherwise.</returns>
		private bool FlavorMatches(File file)
		{
			return _flavorMatchers.TrueForAll(matcher => matcher.Matches(file));
		}

		/// <summary>
		/// Calculates the total weight of a file based on flavor settings.
		/// </summary>
		/// <param name="file">File to check</param>
		/// <returns>Total weight of the file based on the user's flavor settings</returns>
		private int FlavorWeight(File file)
		{
			return _flavorMatchers.Sum(matcher => matcher.Weight(file));
		}
	}
}
