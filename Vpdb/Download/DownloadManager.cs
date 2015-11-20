using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NLog;
using ReactiveUI;
using VpdbAgent.Application;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Vpdb.Download
{
	/// <summary>
	/// Manages how to download stuff, e.g. which files are needed, which flavor
	/// for a release and moves files to the right place after download.
	/// 
	/// Note that the download itself is handled by <see cref="JobManager"/>.
	/// </summary>
	public interface IDownloadManager
	{
		/// <summary>
		/// Downloads or upgrades a release, including table image, ROMs and other 
		/// media, based on the user's settings.
		/// </summary>
		/// <remarks>
		/// File selection is based on the user's preferences. This is typically
		/// called when the user stars a release, where we have no more data
		/// than the release ID. 
		/// </remarks>
		/// <param name="releaseId">ID of the release</param>
		/// <returns>This instance</returns>
		IDownloadManager DownloadRelease(string releaseId);

		/// <summary>
		/// Downloads or upgrades a release, including table image, ROMs and other
		/// media, based on the user's settings.
		/// </summary>
		/// <param name="release">Release to download or upgrade</param>
		/// <param name="tableFile">File of the release to download</param>
		/// <returns>This instance</returns>
		IDownloadManager DownloadRelease(Release release, TableFile tableFile);

		/// <summary>
		/// An observable that produces a value when a release finished 
		/// downloading successfully.
		/// </summary>
		IObservable<Job> WhenReleaseDownloaded { get; }
	}
	
	public class DownloadManager : IDownloadManager
	{
		// dependencies
		private readonly IPlatformManager _platformManager;
		private readonly IJobManager _jobManager;
		private readonly IVpdbClient _vpdbClient;
		private readonly ISettingsManager _settingsManager;
		private readonly Logger _logger;

		// props
		public IObservable<Job> WhenReleaseDownloaded => _whenReleaseDownloaded;

		// members
		private readonly Subject<Job> _whenReleaseDownloaded = new Subject<Job>();
		private readonly List<IFlavorMatcher> _flavorMatchers = new List<IFlavorMatcher>();

		public DownloadManager(IPlatformManager platformManager, IJobManager jobManager, IVpdbClient vpdbClient, ISettingsManager settingsManager, Logger logger)
		{
			_platformManager = platformManager;
			_jobManager = jobManager;
			_vpdbClient = vpdbClient;
			_settingsManager = settingsManager;
			_logger = logger;

			// setup download callbacks
			jobManager.WhenDownloaded.Subscribe(OnDownloadCompleted);

			// setup flavor matchers as soon as settings are available.
			_settingsManager.Settings.WhenAny(
				s => s.DownloadOrientation, 
				s => s.DownloadOrientationFallback, 
				s => s.DownloadLighting, 
				s => s.DownloadLightingFallback, 
				(a, b, c, d) => Unit.Default).Subscribe(_ =>
			{
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
			_vpdbClient.Api.GetFullRelease(id).ObserveOn(Scheduler.Default).Subscribe(release => {

				// match file based on settings
				var file = release.Versions
					.SelectMany(v => v.Files)
					.Where(FlavorMatches)
					.Select(f => new {f, weight = FlavorWeight(f)})
					.OrderBy(x => x.weight)
					.Select(x => x.f)
					.LastOrDefault();

				// check if match
				if (file == null) {
					_logger.Info("Nothing matched current flavor configuration, skipping.");
					return;
				}

				// download
				DownloadRelease(release, file);

			}, exception => _vpdbClient.HandleApiError(exception, "retrieving release details during download"));

			return this;
		}

		public IDownloadManager DownloadRelease(Release release, TableFile tableFile)
		{
			// also fetch game data for media & co
			_vpdbClient.Api.GetGame(release.Game.Id).Subscribe(game => 
			{
				var gameName = release.Game.DisplayName;
				var pbxPlatform = _platformManager.FindPlatform(tableFile);
				var vpdbPlatform = tableFile.Compatibility[0].Platform;

				// check if backglass image needs to be downloaded
				var backglassImagePath = Path.Combine(pbxPlatform.MediaPath, Job.MediaBackglassImages);
				if (!FileBaseExists(backglassImagePath, gameName)) {
					_jobManager.AddJob(new Job(release, game.Media["backglass"], FileType.BackglassImage, vpdbPlatform));
				}

				// check if wheel image needs to be downloaded
				var wheelImagePath = Path.Combine(pbxPlatform.MediaPath, Job.MediaWheelImages);
				if (!FileBaseExists(wheelImagePath, gameName)) {
					_jobManager.AddJob(new Job(release, game.Media["logo"], FileType.WheelImage, vpdbPlatform));
				}

				// queue table shot
				var tableImage = Path.Combine(pbxPlatform.MediaPath, Job.MediaTableImages);
				if (!FileBaseExists(tableImage, gameName)) {
					_jobManager.AddJob(new Job(release, tableFile.Media["playfield_image"], FileType.TableImage, vpdbPlatform));
				}

				// todo check for ROM to be downloaded
				// todo also queue all remaining non-table files of the release.

				// queue for download
				var job = new Job(release, tableFile, FileType.TableFile, vpdbPlatform);
				_jobManager.AddJob(job);
			});
			return this;
		}

		private static bool FileBaseExists(string path, string name)
		{
			return Directory.EnumerateFiles(path, name + ".*").Any();
		}

		/// <summary>
		/// Executed for every file after successful download.
		/// </summary>
		/// <param name="job">The job that finished download</param>
		private void OnDownloadCompleted(Job job)
		{
			// move file to the right place
			MoveDownloadedFile(job, _platformManager.FindPlatform(job.Platform));

			// do other stuff depending on file type
			if (job.FileType == FileType.TableFile) {
				_whenReleaseDownloaded.OnNext(job);
			}
		}

		/// <summary>
		/// Moves a downloaded file to the table folder of the platform.
		/// </summary>
		/// <param name="job">Job of downloaded file</param>
		/// <param name="platform">Platform of the downloaded file</param>
		private void MoveDownloadedFile(Job job, Platform platform)
		{
			// move downloaded file to table folder
			if (job.FilePath != null && File.Exists(job.FilePath)) {
				try {
					var dest = job.GetFileDestination(platform);
					if (dest != null && !File.Exists(dest)) {
						_logger.Info("Moving downloaded file from {0} to {1}...", job.FilePath, dest);
						File.Move(job.FilePath, dest);
					} else {
						// todo see how to handle, probably name it differently.
						_logger.Warn("File {0} already exists at destination!", dest);
					}
				} catch (Exception e) {
					_logger.Error(e, "Error moving downloaded file.");
				}
			} else {
				_logger.Error("Downloaded file {0} does not exist.", job.FilePath);
			}
		}

		/// <summary>
		/// Checks if the flavor of the file is acceptable by the user's flavor settings.
		/// </summary>
		/// <param name="tableFile">File to check</param>
		/// <returns>Returns true if the primary or secondary flavor setting of ALL flavors matches, false otherwise.</returns>
		private bool FlavorMatches(TableFile tableFile)
		{
			return _flavorMatchers.TrueForAll(matcher => matcher.Matches(tableFile));
		}

		/// <summary>
		/// Calculates the total weight of a file based on flavor settings.
		/// </summary>
		/// <param name="tableFile">File to check</param>
		/// <returns>Total weight of the file based on the user's flavor settings</returns>
		private int FlavorWeight(TableFile tableFile)
		{
			return _flavorMatchers.Sum(matcher => matcher.Weight(tableFile));
		}
	}
}
