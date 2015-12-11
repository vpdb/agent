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
	/// The download itself is handled by the <see cref="JobManager"/>.
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
		/// <param name="currentFile">The current/previous file of the release or null if new release</param>
		/// <returns>This instance</returns>
		IDownloadManager DownloadRelease(string releaseId, VpdbTableFile currentFile = null);

		/// <summary>
		/// An observable that produces a value when a release finished 
		/// downloading successfully.
		/// </summary>
		IObservable<Job> WhenReleaseDownloaded { get; }

		/// <summary>
		/// Returns the latest file of a release that matches the user's flavor
		/// preferences or null if nothing found.
		/// 
		/// For existing releases, provide the file of the existing release. This 
		/// makes it possible to match the "Same" rule.
		/// </summary>
		/// <remarks>
		/// Note that if <see cref="currentFile"/> is provided and the lastest file
		/// is the same version as <see cref="currentFile"/>, <c>null</c> is returned.
		/// </remarks>
		/// <param name="release"></param>
		/// <param name="currentFile"></param>
		/// <returns>The most recent file that matches user's flavor prefs and is not the same version as provided, or null if not found</returns>
		VpdbTableFile FindLatestFile(VpdbRelease release, VpdbTableFile currentFile = null);
	}

	public class DownloadManager : IDownloadManager
	{
		// dependencies
		private readonly IPlatformManager _platformManager;
		private readonly IJobManager _jobManager;
		private readonly IVpdbClient _vpdbClient;
		private readonly ISettingsManager _settingsManager;
		private readonly IMessageManager _messageManager;
		private readonly CrashManager _crashManager;
		private readonly Logger _logger;

		// props
		public IObservable<Job> WhenReleaseDownloaded => _whenReleaseDownloaded;

		// members
		private readonly Subject<Job> _whenReleaseDownloaded = new Subject<Job>();
		private readonly List<IFlavorMatcher> _flavorMatchers = new List<IFlavorMatcher>();

		public DownloadManager(IPlatformManager platformManager, IJobManager jobManager, IVpdbClient vpdbClient, 
			ISettingsManager settingsManager, IMessageManager messageManager, CrashManager crashManager, Logger logger)
		{
			_platformManager = platformManager;
			_jobManager = jobManager;
			_vpdbClient = vpdbClient;
			_settingsManager = settingsManager;
			_messageManager = messageManager;
			_crashManager = crashManager;
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

		public IDownloadManager DownloadRelease(string id, VpdbTableFile currentFile = null)
		{
			// retrieve release details
			_logger.Info("Retrieving details for release {0}...", id);
			_vpdbClient.Api.GetFullRelease(id).ObserveOn(Scheduler.Default).Subscribe(release => {

				// match file based on settings
				var file = FindLatestFile(release, currentFile);

				// check if match
				if (file == null) {
					_logger.Info("Nothing matched current flavor configuration, skipping.");
					return;
				}

				var version = release.Versions.FirstOrDefault(v => v.Files.Contains(file));
				_logger.Info($"Found new release to download: v{version?.Name} - {file.Reference.Name}");

				// download
				DownloadRelease(release, file);

			}, exception => _vpdbClient.HandleApiError(exception, "retrieving release details during download"));

			return this;
		}

		public VpdbTableFile FindLatestFile(VpdbRelease release, VpdbTableFile currentFile = null)
		{
			if (release == null) {
				return null;
			}

			var file = release.Versions
					.SelectMany(v => v.Files)
					.Where(f => FlavorMatches(f, currentFile))
					.Select(f => new { f, weight = FlavorWeight(f, currentFile) })
					.OrderBy(x => x.weight)
					.Select(x => x.f)
					.LastOrDefault();

			// check for same version
			if (file != null && currentFile != null) {
				var currentVersion = release.Versions.FirstOrDefault(v => v.Files.Select(f => f.Reference.Id).Contains(currentFile.Reference.Id));
				var latestVersion = release.Versions.FirstOrDefault(v => v.Files.Select(f => f.Reference.Id).Contains(file.Reference.Id));
				if (latestVersion != null && currentVersion != null && currentVersion.Name == latestVersion.Name) {
					return null;
				}
			}
			return file;
		}

		/// <summary>
		/// Downloads a release including media and ROMs.
		/// </summary>
		/// <param name="release">Release to download</param>
		/// <param name="tableFile">File of the release to download</param>
		private void DownloadRelease(VpdbRelease release, VpdbTableFile tableFile)
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

			}, exception => _vpdbClient.HandleApiError(exception, "retrieving game details during download"));
		}

		/// <summary>
		/// Checks whether any file for the given base name at the given path exists.
		/// </summary>
		/// <param name="path">Path to look for</param>
		/// <param name="name">Base name to find</param>
		/// <returns>True if exists, false otherwise</returns>
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
				_messageManager.LogReleaseDownloaded(job.Release, job.Version, job.File, job.TransferredBytes / (job.FinishedAt - job.StartedAt).TotalMilliseconds * 1000);
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
					_crashManager.Report(e, "fs");
				}
			} else {
				_logger.Error("Downloaded file {0} does not exist.", job.FilePath);
			}
		}

		/// <summary>
		/// Checks if the flavor of the file is acceptable by the user's flavor settings.
		/// </summary>
		/// <param name="tableFile">File to check</param>
		/// <param name="currentFile">The current/previous file of the release or null if new release</param>
		/// <returns>Returns true if the primary or secondary flavor setting of ALL flavors matches, false otherwise.</returns>
		private bool FlavorMatches(VpdbTableFile tableFile, VpdbTableFile currentFile)
		{
			return _flavorMatchers.TrueForAll(matcher => matcher.Matches(tableFile, currentFile));
		}

		/// <summary>
		/// Calculates the total weight of a file based on flavor settings.
		/// </summary>
		/// <param name="tableFile">File to check</param>
		/// <param name="currentFile">The current/previous file of the release or null if new release</param>
		/// <returns>Total weight of the file based on the user's flavor settings</returns>
		private int FlavorWeight(VpdbTableFile tableFile, VpdbTableFile currentFile)
		{
			return _flavorMatchers.Sum(matcher => matcher.Weight(tableFile, currentFile));
		}
	}
}
