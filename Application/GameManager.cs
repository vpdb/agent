using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using NLog;
using ReactiveUI;
using SynchrotronNet;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.VisualPinball;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Download;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.Application
{
	/// <summary>
	/// Our internal game API.
	/// 
	/// It manages the games that are defined in the user's XML database of
	/// PinballX. However, since we're dealing with more data than what's in
	/// those XMLs, we keep our own data structure in JSON files.
	/// 
	/// JSON files are system-specific, meaning for every system defined in
	/// PinballX (we call them "Platforms"), there is one JSON file. Games
	/// are always linked to the respective system so we know where to retrieve
	/// table files, media etc.
	/// 
	/// This is how the JSON files are generated:
	///   1. GameManager instantiates MenuManager which parses PinballX.ini
	///   2. GameManager loops through parsed systems and retrieves local vpdb.jsons
	///   3. GameManager merges games from MenuManager and vpdb.json to new vpdb.jsons
	///   4. GameManager dumps new vpdb.jsons
	/// 
	/// Everything is event- or subscription based, since we want to automatically
	/// repeat the process when relevant files change. 
	/// </summary>
	public interface IGameManager
	{
		/// <summary>
		/// Games are 2-way mapped to <see cref="Game"/>, where downstream 
		/// changes (e.g. XMLs in PinballX's database folder change) come from
		/// <see cref="PinballXGame"/> and upstream changes are written
		/// to the .json file sitting in the system's database folder.
		/// </summary>
		ReactiveList<Game> Games { get; }

		/// <summary>
		/// A one-time message fired when everything has been initialized and
		/// GUI can start adding its own subscriptions without re-updating
		/// during initialization.
		/// </summary>
		IObservable<Unit> Initialized { get; }

		/// <summary>
		/// Initializes the menu manager, which basically starts watching
		/// relevant files.
		/// </summary>
		/// <returns>This instance</returns>
		void Initialize();

		/// <summary>
		/// Links a release from VPDB to a game.
		/// </summary>
		/// <param name="game">Local game to link to</param>
		/// <param name="release">Release from VPDB</param>
		/// <param name="fileId">File ID at VPDB</param>
		/// <returns>This instance</returns>
		void LinkRelease(Game game, VpdbRelease release, string fileId);

		/// <summary>
		/// Explicitly enables syncing of a game.
		/// </summary>
		/// <remarks>
		/// Results in an immediate update if available.
		/// </remarks>
		/// <param name="game">Game to synchronize</param>
		/// <returns>This instance</returns>
		IGameManager Sync(Game game);
	}

	/// <summary>
	/// Application logic for <see cref="IGameManager"/>.
	/// </summary>
	public class GameManager : IGameManager
	{
		// deps
		private readonly IMenuManager _menuManager;
		private readonly IVpdbClient _vpdbClient;
		private readonly ISettingsManager _settingsManager;
		private readonly IDownloadManager _downloadManager;
		private readonly IDatabaseManager _databaseManager;
		private readonly IVersionManager _versionManager;
		private readonly IPlatformManager _platformManager;
		private readonly IMessageManager _messageManager;
		private readonly IRealtimeManager _realtimeManager;
		private readonly IVisualPinballManager _visualPinballManager;
		private readonly Logger _logger;

		// props
		public ReactiveList<Game> Games { get; } = new ReactiveList<Game>();
		public IObservable<Unit> Initialized => _initialized;

		// privates
		private readonly Subject<Unit> _initialized = new Subject<Unit>();
		private bool _isInitialized;
		private readonly List<Tuple<string, string, string>> _gamesToLink = new List<Tuple<string, string, string>>();

		public GameManager(IMenuManager menuManager, IVpdbClient vpdbClient, ISettingsManager 
			settingsManager, IDownloadManager downloadManager, IDatabaseManager databaseManager,
			IVersionManager versionManager, IPlatformManager platformManager, IMessageManager messageManager,
			IRealtimeManager realtimeManager, IVisualPinballManager visualPinballManager, Logger logger)
		{
			_menuManager = menuManager;
			_vpdbClient = vpdbClient;
			_settingsManager = settingsManager;
			_downloadManager = downloadManager;
			_databaseManager = databaseManager;
			_versionManager = versionManager;
			_platformManager = platformManager;
			_messageManager = messageManager;
			_realtimeManager = realtimeManager;
			_visualPinballManager = visualPinballManager;
			_logger = logger;

			// setup game change listener once all games are fetched.
			_menuManager.Initialized.Subscribe(_ => SetupGameChanges());

			// update releases from VPDB on the first run, but delay it a bit so it 
			// doesn't do all that shit at the same time!
			_settingsManager.ApiAuthenticated
				.Where(user => user != null)
				.Take(1)
				.Delay(TimeSpan.FromSeconds(2))
				.Subscribe(_ => UpdateReleaseData());

			// subscribe to downloaded releases
			_downloadManager.WhenReleaseDownloaded.Subscribe(OnReleaseDownloaded);

			// link games if new games are added 
			Games.Changed.Subscribe(_ => CheckGameLinks());

			// setup handlers for table file changes
			_menuManager.TableFileChanged.Subscribe(OnTableFileChanged);
			_menuManager.TableFileRemoved.Subscribe(OnTableFileRemoved);

			// when game is linked or unlinked, update profile with channel info
			IDisposable gameLinked = null;
			Games.Changed.Subscribe(_ => {
				gameLinked?.Dispose();
				gameLinked = Games
					.Select(g => g.Changed.Where(x => x.PropertyName == "ReleaseId"))
					.Merge()
					.Subscribe(__ => UpdateChannelConfig());
			});

			// setup pusher messages
			SetupRealtime();
		}

		public void Initialize()
		{
			// settings must be initialized before doing this.
			if (string.IsNullOrEmpty(_settingsManager.Settings.ApiKey)) {
				throw new InvalidOperationException("Must initialize settings before game manager.");
			}

			_databaseManager.Initialize();
			_menuManager.Initialize();
			_vpdbClient.Initialize();
			_versionManager.Initialize();

			// validate settings and retrieve profile
			Task.Run(async () => await _settingsManager.Validate(_settingsManager.Settings, _messageManager));
		}

		public void LinkRelease(Game game, VpdbRelease release, string fileId)
		{
			// update in case we didn't catch the last version.
			_vpdbClient.Api.GetRelease(release.Id).Subscribe(updatedRelease => {
				_logger.Info("Linking {0} to {1} ({2})", game, release, fileId);
				_databaseManager.AddOrUpdateRelease(release);
				game.ReleaseId = release.Id;
				game.FileId = fileId;
				_databaseManager.Save();

			}, exception => _vpdbClient.HandleApiError(exception, "retrieving release details during linking"));
		}

		public IGameManager Sync(Game game)
		{
			_downloadManager.DownloadRelease(game.ReleaseId, game.FileId);
			return this;
		}

		/// <summary>
		/// Sets up a listener that updates our global game list when either games
		/// added or removed, or platforms are added or removed.
		/// </summary>
		/// <seealso cref="http://stackoverflow.com/questions/15254708/"/>
		/// <remarks>
		/// This 
		/// </remarks>
		private void SetupGameChanges()
		{
			// here we push all games in all platforms into the Games list.
			var whenPlatformsOrGamesInThosePlatformsChange = Observable.Merge(
				_platformManager.Platforms.Changed                                                      // one of the games changes
					.SelectMany(_ => _platformManager.Platforms.Select(x => x.Games.Changed).Merge())
					.Select(_ => Unit.Default),
				_platformManager.Platforms.Changed.Select(_ => Unit.Default));                          // one of the platforms changes

			whenPlatformsOrGamesInThosePlatformsChange
				.StartWith(Unit.Default)
				.Select(_ => _platformManager.Platforms.SelectMany(x => x.Games).ToList())
				.Where(games => games.Count > 0)
				.Subscribe(games => {
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						// TODO better logic
						using (Games.SuppressChangeNotifications()) {
							Games.Clear();
							Games.AddRange(games);
						}
						_logger.Info("Set {0} games.", games.Count);

						if (!_isInitialized) {
							_initialized.OnNext(Unit.Default);
							_initialized.OnCompleted();
							_isInitialized = true;
						}
					});
				});
		}

		/// <summary>
		/// Sets up what happens when realtime messages from Pusher arrive.
		/// </summary>
		private void SetupRealtime()
		{
			// starring
			_realtimeManager.WhenReleaseStarred.Subscribe(msg => {

				// release starred
				if (msg.Starred) {
					var game = OnStarRelease(msg.Id, true);
					if (game == null) {
						if (_settingsManager.Settings.SyncStarred) {
							_downloadManager.DownloadRelease(msg.Id);
						} else {
							_logger.Info("Sync starred not enabled, ignoring starred release.");
						}
					}

				// release unstarred
				} else {
					OnStarRelease(msg.Id, false);
				}
			});

			// new release version
			_realtimeManager.WhenReleaseUpdated.Subscribe(msg => {
				var game = Games.FirstOrDefault(g => g.ReleaseId == msg.ReleaseId);
				if (game != null) {
					_vpdbClient.Api.GetFullRelease(msg.ReleaseId)
						.Subscribe(_databaseManager.AddOrUpdateRelease,
							exception => _vpdbClient.HandleApiError(exception, "while retrieving updated release"));
				} else {
					_logger.Warn("Got update from non-existent release {0}.", msg.ReleaseId);
				}
			});
		}

		/// <summary>
		/// Updates the channel config of the user's profile at VPDB.
		/// 
		/// This basically tells the server to send release events through pusher
		/// of non-starred releases or all releases if sync-starring is disabled.
		/// </summary>
		/// <remarks>
		/// Executed if either a game's <see cref="Game.IsSynced"/> changes or the
		/// setting's <see cref="Settings.SyncStarred"/>.
		/// </remarks>
		private void UpdateChannelConfig()
		{	
			// settings not initialized or other auth error
			if (_settingsManager.AuthenticatedUser == null) {
				return;
			}

			// subscribe all linked releases (we want to at least know about updates for non-synched releases)
			_settingsManager.AuthenticatedUser.ChannelConfig.SubscribedReleases = Games
					.Where(g => !string.IsNullOrEmpty(g.ReleaseId))
					.Select(g => g.ReleaseId)
					.ToList();

			_settingsManager.AuthenticatedUser.ChannelConfig.SubscribeToStarred = _settingsManager.Settings.SyncStarred;
			_vpdbClient.Api.UpdateProfile(_settingsManager.AuthenticatedUser).Subscribe(user => {
				_logger.Info("Updated channel profile: {0}, [{1}]", _settingsManager.Settings.SyncStarred,
					string.Join(",", _settingsManager.AuthenticatedUser.ChannelConfig.SubscribedReleases));
			}, exception => _vpdbClient.HandleApiError(exception, "updating profile with channel info"));
		}

		/// <summary>
		/// Checks if any games are to be linked to a release. Executed each time
		/// games change.
		/// </summary>
		/// See <see cref="AddGame"/> for an explanation.
		private void CheckGameLinks()
		{
			if (_gamesToLink.Count > 0) {
				for (var i = _gamesToLink.Count - 1; i >= 0; i--) {
					var x = _gamesToLink[i];
					var game = Games.FirstOrDefault(g => g.Id.Equals(x.Item1));
					var release = _databaseManager.GetRelease(x.Item2);
					LinkRelease(game, release, x.Item3);
					_gamesToLink.RemoveAt(i); 
				}
			}
		}

		/// <summary>
		/// Retrieves all known releases from the VPDB API and updates them locally.
		/// 
		/// Executed when the application starts in order to synchronize with VPDB.
		/// todo include file id when searching. (also needs update on backend.)
		/// </summary>
		private void UpdateReleaseData()
		{
			// get local release ids
			var releaseIds = Games.Where(g => g.HasRelease).Select(g => g.ReleaseId).ToList();
			if (releaseIds.Count > 0) {
				_logger.Info("Updating {0} release(s)", releaseIds.Count);

				// retrieve all releases of local ids
				_vpdbClient.Api.GetReleasesByIds(string.Join(",", releaseIds))
					.SubscribeOn(Scheduler.Default)
					.Subscribe(releases => {
						
						// update release data
						foreach (var release in releases) {
							_databaseManager.AddOrUpdateRelease(release);
						}

						// save
						_databaseManager.Save();
					}, exception => _vpdbClient.HandleApiError(exception, "retrieving all known releases by ID"));
			} else {
				_logger.Info("Skipping release update, no linked releases found.");
			}
		}

		/// <summary>
		/// Adds or updates the XML database of PinballX. Also updates fileId
		/// if the download was an update.
		/// 
		/// Executed after a release has been successfully downloaded.
		/// </summary>
		/// <param name="job">Download job that finished</param>
		private void OnReleaseDownloaded(Job job)
		{
			// find release locally
			var game = Games.FirstOrDefault(g => g.ReleaseId == job.ReleaseId);

			// add release
			_databaseManager.Save();

			// add or update depending if found or not
			if (game == null) {
				AddGame(job);

			} else {
				var previousFileId = game.FileId;
				var previousFilename = game.Filename;
				var from = _databaseManager.GetVersion(job.ReleaseId, game.FileId);
				var to = _databaseManager.GetVersion(job.ReleaseId, job.FileId);
				_logger.Info("Updating file ID from {0} ({1}) to {2} ({3})...", game.FileId, from, job.FileId, to);
				game.FileId = job.FileId;
				UpdateGame(game, job);

				if (_settingsManager.Settings.PatchTableScripts) {
					PatchGame(game, previousFileId, previousFilename, job.FileId);
				}
			}
		}

		private void PatchGame(Game game, string baseFileId, string baseFileName, string fileToPatchId)
		{
			_logger.Info("Patching file {0} with changes from file {1}", fileToPatchId, baseFileId);

			// get table scripts for original files
			var baseFile = _databaseManager.GetTableFile(game.ReleaseId, baseFileId).Reference;
			var fileToPatch = _databaseManager.GetTableFile(game.ReleaseId, fileToPatchId).Reference;
			var baseScript = baseFile?.Metadata["table_script"];
			var scriptToPatch = fileToPatch?.Metadata["table_script"];
			if (baseScript == null || scriptToPatch == null) {
				_logger.Warn("Got no script for file {0}, aborting.", baseScript == null ? baseFileId : fileToPatchId);
				return;
			}

			// get script from local (potentially modified) table file
			var oldTablePath = Path.Combine(game.Platform.TablePath, baseFileName);
			var localScript = _visualPinballManager.GetTableScript(oldTablePath);
			if (localScript == null) {
				_logger.Warn("Error reading table script from {0}.", oldTablePath);
			}

			if (localScript == baseScript) {
				_logger.Warn("Got no script for file {0}, aborting.", baseScript == null ? baseFileId : fileToPatchId);
				return;
			}

			// sanity check: compare extracted script from vpdb with our own
			var newTablePath = Path.Combine(game.Platform.TablePath, game.Filename);
			var newScript = _visualPinballManager.GetTableScript(newTablePath);
			if (newScript != scriptToPatch)
			{
				_logger.Error("Script from VPDB ({0} bytes) is not identical to what we've extracted from the download ({1} bytes).", scriptToPatch.Length, newScript.Length);
				return;
			}

			var baseScriptLines = baseScript.Split('\n');
			var scriptToPatchLines = baseScript.Split('\n');
			var localScriptLines = baseScript.Split('\n');

			// do the three-way merge
			var result = Diff.Diff3Merge(localScriptLines, baseScriptLines, scriptToPatchLines, true);
			var patchedScriptLines = new List<string>();
			var failed = 0;
			var succeeded = 0;
			foreach (var block in result) {
				var okBlock = block as Diff.MergeOkResultBlock;
				var conflictBlock = block as Diff.MergeConflictResultBlock;
				if (okBlock != null) {
					succeeded++;
					patchedScriptLines.AddRange(okBlock.ContentLines);
					//Console.WriteLine("------------------- Success: \n{0}", string.Join("\n", okBlock.ContentLines));

				} else if (conflictBlock != null) {
					failed++;
					//Console.WriteLine("------------------- Conflict.");

				} else {
					throw new InvalidOperationException("Result must be either ok or conflict.");
				}
			}
			if (failed > 0) {
				_logger.Warn("Patching failed ({0} block(s) ok, {1} block(s) conflicted.", succeeded, failed);
				return;
			}
			var patchedScript = string.Join("\n", patchedScriptLines);
			_logger.Info("Successfully patched scripts ({0} block(s) applied).", succeeded);

			// write back script
			try {
				_visualPinballManager.SetTableScript(newTablePath, patchedScript);
			} catch (Exception e) {
				_logger.Error(e, "Error writing patched script back to table file.");
				return;
			}
			_logger.Info("Successfully wrote back script to table file.");
		}

		/// <summary>
		/// A table file has been changed or added (or renamed to given path).
		/// </summary>
		/// <param name="path">Absolute path of the file</param>
		private void OnTableFileChanged(string path)
		{
			Games
				.Where(g => g.Filename != null)
				.Where(g => Path.GetFileNameWithoutExtension(g.Filename).Equals(Path.GetFileNameWithoutExtension(path)))
				.ToList()
				.ForEach(g => { g.Exists = true; });
		}

		/// <summary>
		/// A table file has been deleted (or renamed from given path).
		/// </summary>
		/// <param name="path">Absolute path of the file</param>
		private void OnTableFileRemoved(string path)
		{
			Games
				.Where(g => g.Filename != null)
				.Where(g => g.Filename.Equals(Path.GetFileName(path)))
				.ToList()
				.ForEach(g => { g.Exists = false; });
		}

		/// <summary>
		/// Adds a downloaded game to the PinballX database.
		/// </summary>
		/// <param name="job">Job of the downloaded game</param>
		private void AddGame(Job job)
		{
			_logger.Info("Adding {0} to PinballX database...", job.Release);

			var tableFile = _databaseManager.GetTableFile(job.ReleaseId, job.FileId);
			var platform = _platformManager.FindPlatform(tableFile);
			if (platform == null) {
				_logger.Warn("Cannot find platform for release {0} ({1}), aborting.", job.Release.Id, string.Join(",", tableFile.Compatibility));
				return;
			}
			var newGame = _menuManager.NewGame(job);

			// adding the game (updating the xml) forces a new rescan. but it's
			// async so in order to avoid race conditions, we put this into a 
			// "linking" queue, meaning on the next update, it will also get 
			// linked.
			_gamesToLink.Add(new Tuple<string, string, string>(newGame.Description, job.Release.Id, job.File.Id));

			// save new game to Vpdb.xml (and trigger rescan)
			_menuManager.AddGame(newGame, platform.DatabasePath);
		}

		/// <summary>
		/// Updates an existing game in the PinballX database
		/// </summary>
		/// <remarks>
		/// Usually happends when a game is updated to a new version.
		/// </remarks>
		/// <param name="game">Game to be updated</param>
		/// <param name="job">Job of downloaded game</param>
		private void UpdateGame(Game game, Job job)
		{
			_logger.Info("Updating {0} in PinballX database...", job.Release);

			var oldFileName = Path.GetFileNameWithoutExtension(game.Filename);
				
			// update and save json
			game.Filename = Path.GetFileName(job.FilePath);

			// update and save xml
			_menuManager.UpdateGame(oldFileName, game);
		}

		/// <summary>
		/// Toggles star on a release
		/// </summary>
		/// <param name="releaseId">Release ID</param>
		/// <param name="starred">If true, star, otherwise unstar</param>
		/// <returns>Local game if found, null otherwise</returns>
		private Game OnStarRelease(string releaseId, bool starred)
		{
			var game = Games.FirstOrDefault(g => !string.IsNullOrEmpty(g.ReleaseId) && g.ReleaseId.Equals(releaseId));
			if (game != null) {
				var release = _databaseManager.GetRelease(releaseId);
				release.Starred = starred;
				if (_settingsManager.Settings.SyncStarred) {
					game.IsSynced = starred;
				}
				_databaseManager.Save();
				_logger.Info("Toggled star on release {0} [{1}]", release.Name, starred ? "on" : "off");
			}
			return game;
		}
	}
}