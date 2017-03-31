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
using VpdbAgent.Common.Filesystem;
using VpdbAgent.Data;
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
	/// This deals with aggregating data from the different sources but also
	/// acts as an entry point for the UI to do certain tasks.
	/// </summary>
	public interface IGameManager
	{
		/// <summary>
		/// A list we call "Global Games" aka the important stuff.
		/// </summary>
		/// 
		/// <remarks>
		/// This lists consists of data from the file system (table files), the
		/// PinballX XML database files, and Mappings from `vpdb.json`.
		/// </remarks>
		ReactiveList<AggregatedGame> AggregatedGames { get; }

		/// <summary>
		/// A one-time message fired when everything has been initialized and
		/// GUI can start adding its own subscriptions without re-updating
		/// during initialization.
		/// </summary>
		IObservable<Unit> Initialized { get; }

		/// <summary>
		/// While all the services can be instantiated at application start-up,
		/// we need some conditions met (configuration) before we can start
		/// with the business logic (namely watchin stuff on the file system).
		/// 
		/// Run this when configuration is set and validated.
		/// </summary>
		/// <returns>This instance</returns>
		void Initialize();

		/// <summary>
		/// Marks a game as hidden.
		/// </summary>
		/// 
		/// <remarks>
		/// Since we usually don't know which system this file would belong to, we 
		/// just take the first system which matches the table path.
		/// </remarks>
		/// <param name="game">Game to hide</param>
		void HideGame(AggregatedGame game);

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
		private readonly IThreadManager _threadManager;
		private readonly IFile _file;
		private readonly ILogger _logger;

		// props
		public ReactiveList<AggregatedGame> AggregatedGames { get; } = new ReactiveList<AggregatedGame> { ChangeTrackingEnabled = true };
		public IObservable<Unit> Initialized => _initialized;

		// privates
		private readonly Subject<Unit> _initialized = new Subject<Unit>();
		private readonly List<Tuple<string, string, string>> _gamesToLink = new List<Tuple<string, string, string>>();

		public GameManager(IMenuManager menuManager, IVpdbClient vpdbClient, ISettingsManager 
			settingsManager, IDownloadManager downloadManager, IDatabaseManager databaseManager,
			IVersionManager versionManager, IPlatformManager platformManager, IMessageManager messageManager,
			IRealtimeManager realtimeManager, IVisualPinballManager visualPinballManager, IThreadManager threadManager, 
			IFile file, ILogger logger)
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
			_threadManager = threadManager;
			_file = file;
			_logger = logger;

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
			//Games.Changed.Subscribe(_ => CheckGameLinks());

			// when game is linked or unlinked, update profile with channel info
			/*IDisposable gameLinked = null;
			Games.Changed.Subscribe(_ => {
				gameLinked?.Dispose();
				gameLinked = Games
					.Select(g => g.Changed.Where(x => x.PropertyName == "ReleaseId"))
					.Merge()
					.Subscribe(__ => UpdateChannelConfig());
			});*/

			// setup pusher messages
			SetupRealtime();
		}

		public void Initialize()
		{
			// settings must be initialized before doing this.
			if (string.IsNullOrEmpty(_settingsManager.Settings.ApiKey)) {
				throw new InvalidOperationException("Must initialize settings before game manager.");
			}

			var delay = TimeSpan.FromMilliseconds(500);   // let props update first

			// setup handlers for table file changes TODO implement properly (specially rename)
			_menuManager.TableFileCreated.ObserveOn(_threadManager.WorkerScheduler).Subscribe(OnTableFileChanged);
			_menuManager.TableFileChanged.ObserveOn(_threadManager.WorkerScheduler).Subscribe(OnTableFileChanged);
			_menuManager.TableFileDeleted.ObserveOn(_threadManager.WorkerScheduler).Subscribe(OnTableFileDeleted);
			_menuManager.TableFileRenamed.ObserveOn(_threadManager.WorkerScheduler).Subscribe(x => {
				OnTableFileDeleted(x.Item1);
				OnTableFileChanged(x.Item2);
			});
			_menuManager.TableFolderAdded.ObserveOn(_threadManager.WorkerScheduler).Delay(delay).Subscribe(path => MergeLocalFiles(path, _menuManager.GetTableFiles(path)));
			_menuManager.TableFolderRemoved.ObserveOn(_threadManager.WorkerScheduler).Delay(delay).Subscribe(path => MergeLocalFiles(path, new List<string>()));

			// setup handler for xml database changes
			_menuManager.GamesUpdated.Subscribe(d => MergeXmlGames(d.Item1, d.Item2, d.Item3));

			// setup handler for Matching changes
			_menuManager.MappingsUpdated.Subscribe(d => MergeMappings(d.Item1, d.Item2));

			_databaseManager.Initialize();
			_menuManager.Initialize();
			_vpdbClient.Initialize();
			_versionManager.Initialize();
			_initialized.OnNext(Unit.Default);

			// validate settings and retrieve profile
			Task.Run(async () => await _settingsManager.Validate(_settingsManager.Settings, _messageManager));
		}

		/// <summary>
		/// Merges changed games from PinballX into Global Games.
		/// </summary>
		/// 
		/// <remarks>
		/// The provided games list is exhaustive, meaning existing games of
		/// the given system and database file are to be removed. Provide an
		/// empty list to remove all of them.
		/// 
		/// This is triggered on <see cref="PinballXSystem.GamesUpdated"/>. The
		/// received list is then merged into <see cref="AggregatedGames"/>,
		/// while only data that has changed is updated.
		/// </remarks>
		/// 
		/// <param name="system">System of the updated games</param>
		/// <param name="databaseFile">Database file of the updated games</param>
		/// <param name="xmlGames">Parsed games</param>
		public void MergeXmlGames(PinballXSystem system, string databaseFile, List<PinballXGame> xmlGames)
		{
			lock (AggregatedGames) {

				// if Global Games are empty, don't bother identifiying and add them all
				if (AggregatedGames.IsEmpty) {
					_threadManager.MainDispatcher.Invoke(() => {
						using (AggregatedGames.SuppressChangeNotifications()) {
							AggregatedGames.AddRange(xmlGames.Select(g => new AggregatedGame(g, _file)));
						}
					});
					_logger.Info("Added {0} games from PinballX database.", xmlGames.Count());
					return;
				}

				// otherwise, retrieve the system's games from Global Games.
				var selectedGames = AggregatedGames.Where(g => 
					g.XmlGame != null &&
					ReferenceEquals(g.XmlGame.System, system) && 
					g.XmlGame.DatabaseFile == databaseFile
				).ToList();

				var remainingGames = new HashSet<AggregatedGame>(selectedGames); 
				var gamesToAdd = new List<AggregatedGame>();
				var updated = 0;
				var removed = 0;
				var cleared = 0;

				// update games
				foreach (var newGame in xmlGames) {

					// todo could also use an index
					var fileId = Path.Combine(newGame.System.TablePath, Path.GetFileName(newGame.Filename));
					var oldGame = AggregatedGames.FirstOrDefault(g => g.FileId == fileId);
				
					// no match, add
					if (oldGame == null) {
						gamesToAdd.Add(new AggregatedGame(newGame, _file));

					// match and not equal, so update.
					} else if (!oldGame.EqualsXmlGame(newGame)) {
						_threadManager.MainDispatcher.Invoke(() => oldGame.Update(newGame));
						remainingGames.Remove(oldGame);
						updated++;

					// match but equal, ignore.
					} else {
						remainingGames.Remove(oldGame);
					}
				}

				// add games
				if (gamesToAdd.Count > 0) {
					_threadManager.MainDispatcher.Invoke(() => AggregatedGames.AddRange(gamesToAdd));
				}

				// remove or clear games
				_threadManager.MainDispatcher.Invoke(() => {
					foreach (var game in remainingGames) {
						if (!game.HasLocalFile && !game.HasMapping) {
							AggregatedGames.Remove(game);
							removed++;
						} else {
							cleared++;
							game.ClearXmlGame();
						}
					}
				});

				// done!
				_logger.Info("Added {0} games, removed {1} ({2}), updated {3} from PinballX database.", gamesToAdd.Count, removed, cleared, updated);
			}
		}

		/// <summary>
		/// Merges table files of a table folder into Global Games.
		/// </summary>
		/// 
		/// <remarks>
		/// The provided games list is exhaustive, meaning existing games 
		/// within the given path are to be removed. Provide an empty list to 
		/// remove all of them.
		/// 
		/// This is triggered on <see cref="MenuManager.TableFolderAdded"/> and
		/// <see cref="MenuManager.TableFolderRemoved"/>. The received list is 
		/// then merged into <see cref="AggregatedGames"/>, while only data that
		/// has changed is updated.
		/// </remarks>
		/// <param name="tablePath">Absolute path to table folder</param>
		/// <param name="filePaths">List of absolute paths to table files</param>
		private void MergeLocalFiles(string tablePath, IEnumerable<string> filePaths)
		{
			lock (AggregatedGames) {

				tablePath = PathHelper.NormalizePath(tablePath);
				var selectedGames = AggregatedGames.Where(g => 
					g.HasLocalFile && 
					PathHelper.NormalizePath(Path.GetDirectoryName(g.FileId)) == tablePath
				).ToList();
				var remainingGames = new HashSet<AggregatedGame>(selectedGames);
				var gamesToAdd = new List<AggregatedGame>();
				var updated = 0;
				var removed = 0;
				var cleared = 0;

				// update games
				foreach (var newPath in filePaths) {
					var found = false;

					// todo use fileid-based index of O(n) instead of current O(n^2)
					AggregatedGames
						.Where(game => game.EqualsFileId(newPath))
						.ToList()
						.ForEach(oldGame => {
							found = true;
							updated++;
							_threadManager.MainDispatcher.Invoke(() => oldGame.Update(newPath));
							if (remainingGames.Contains(oldGame)) {
								remainingGames.Remove(oldGame);
							}
						});
					if (!found) {
						gamesToAdd.Add(new AggregatedGame(newPath, _file));
					}
				}

				// add games
				if (gamesToAdd.Count > 0) {
					_threadManager.MainDispatcher.Invoke(() => AggregatedGames.AddRange(gamesToAdd));
				}

				// remove or clear games
				_threadManager.MainDispatcher.Invoke(() => {
					foreach (var game in remainingGames) {
						if (!game.HasXmlGame && !game.HasMapping) {
							AggregatedGames.Remove(game);
							removed++;
						} else {
							cleared++;
							game.ClearLocalFile();
						}
					}
				});

				// done!
				_logger.Info("Added {0} games, removed {1} ({2}), updated {3} from file system.", gamesToAdd.Count, removed, cleared, updated);
			}
		}

		private void MergeMappings(PinballXSystem system, IEnumerable<Mapping> mappings)
		{
			// merge shit
			_logger.Info("---- {1}: Mapping changed, new mappings: {0} ({2})", mappings.Count(), system, system.Enabled);
		}

		/// <summary>
		/// A table file has been changed or created.
		/// </summary>
		/// <param name="path">Absolute path of the file</param>
		private void OnTableFileChanged(string path)
		{
			lock (AggregatedGames) {
				var found = false;
				// todo use fileid-based index
				AggregatedGames
					.Where(game => game.EqualsFileId(path))
					.ToList()
					.ForEach(oldGame => {
						found = true;
						_threadManager.MainDispatcher.Invoke(() => oldGame.Update(path));
						if (oldGame.XmlGame == null) {
							_logger.Info("Updated {0} from file system.", path);
						} else {
							_logger.Info("Updated {0} from file system ({1}).", path, oldGame.XmlGame.Description);
						}
					});
				if (!found) {
					_threadManager.MainDispatcher.Invoke(() => AggregatedGames.Add(new AggregatedGame(path, _file)));
					_logger.Info("Added {0} from file system.", path);
				}
			}
		}

		/// <summary>
		/// A table file has been deleted (or renamed from given path).
		/// </summary>
		/// <param name="path">Absolute path of the file</param>
		private void OnTableFileDeleted(string path)
		{
			lock (AggregatedGames) {
				var matchedGames = AggregatedGames.Where(game => game.EqualsFileId(path)).ToList();
				// remove or clear games
				_threadManager.MainDispatcher.Invoke(() => {
					foreach (var game in matchedGames) {
						if (!game.HasXmlGame && !game.HasMapping) {
							AggregatedGames.Remove(game);
							_logger.Info("Removed {0} from file system.", path);
						} else {
							game.ClearLocalFile();
							_logger.Info("Cleared {0} from file system.", path);
						}
					}
				});
			}
		}

		public void HideGame(AggregatedGame game)
		{
			_logger.Info("Hiding game {0}", game.FileId);

			if (game.FilePath == null) {
				_logger.Error("Cannot hide game without local file.");
				return;
			}
			
			PinballXSystem system;
			if (!game.HasSystem) {
				var tablePath = PathHelper.NormalizePath(Path.GetDirectoryName(game.FilePath));
				system = _menuManager.Systems.FirstOrDefault(s => s.TablePath == tablePath);
			} else {
				system = game.System;
			}

			if (system == null) {
				throw new Exception($"Got game at {game.FilePath} but no systems that match path ({string.Join(", ", _menuManager.Systems.Select(s => s.TablePath))}).");
			}

			var mapping = game.Mapping ?? new Mapping();
			mapping.IsHidden = true;

			if (!game.HasMapping) {
				system.Mappings.Add(mapping);
			}
		}



		// --------------------------------------------------------------------------------------------------------------
		// --------------------------------------------------------------------------------------------------------------
		// still dragons below

		public ReactiveList<Game> Games { get; } = new ReactiveList<Game>();

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
		/// Sets up what happens when realtime messages from Pusher arrive.
		/// </summary>
		private void SetupRealtime()
		{
			// starring
			_realtimeManager.WhenReleaseStarred.Subscribe(msg => {

				_logger.Debug("Got star for release {0}", msg.Id);

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
				var previousFilename = game.Filename;
				var from = _databaseManager.GetVersion(job.ReleaseId, game.FileId);
				var to = _databaseManager.GetVersion(job.ReleaseId, job.FileId);
				_logger.Info("Updating file ID from {0} ({1}) to {2} ({3})...", game.FileId, from, job.FileId, to);
				using (game.SuppressChangeNotifications()) {
					game.PreviousFileId = game.FileId;
					game.FileId = job.FileId;
				}
				UpdateGame(game, job);

				if (_settingsManager.Settings.PatchTableScripts) {
					PatchGame(game, previousFilename, job.FileId);
				}
			}
		}

		/// <summary>
		/// Applies table script changes from a previous version to an updated version.
		/// </summary>
		/// <param name="game">Game where to apply changes to</param>
		/// <param name="baseFileName">File name of the previous version</param>
		/// <param name="fileToPatchId">File ID of the updated version</param>
		private void PatchGame(Game game, string baseFileName, string fileToPatchId)
		{
			// todo create log message when something goes wrong.
			/*
			var baseFileId = game.PreviousFileId;
			_logger.Info("Patching file {0} with changes from file {1}", fileToPatchId, baseFileId);

			// get table scripts for original files
			var baseFile = _databaseManager.GetTableFile(game.ReleaseId, baseFileId).Reference;
			var fileToPatch = _databaseManager.GetTableFile(game.ReleaseId, fileToPatchId).Reference;
			var originalBaseScript = (string)baseFile?.Metadata["table_script"];
			var originalScriptToPatch = (string)fileToPatch?.Metadata["table_script"];
			if (originalBaseScript == null || originalScriptToPatch == null) {
				_logger.Warn("Got no script for file {0}, aborting.", originalBaseScript == null ? baseFileId : fileToPatchId);
				return;
			}

			// get script from local (potentially modified) table file
			var localBaseFilePath = Path.Combine(game.Platform.TablePath, baseFileName);
			var localBaseScript = _visualPinballManager.GetTableScript(localBaseFilePath);
			if (localBaseScript == null) {
				_logger.Warn("Error reading table script from {0}.", localBaseFilePath);
				return;
			}

			if (localBaseScript == originalBaseScript) {
				_logger.Info("No changes between old local and remote version, so nothing to patch. We're done patching.");
				return;
			}
			_logger.Info("Script changes between old local and old remote table detected, so let's merge those changes!");

			// sanity check: compare extracted script from vpdb with our own
			var localFilePathToPatch = Path.Combine(game.Platform.TablePath, game.Filename);
			var localScriptToPatch = _visualPinballManager.GetTableScript(localFilePathToPatch);
			if (localScriptToPatch != originalScriptToPatch) {
				_logger.Error("Script in metadata ({0} bytes) is not identical to what we've extracted from the download ({1} bytes).", originalScriptToPatch.Length, localScriptToPatch.Length);
				return;
			}

			// we need line arrays for the merge tool
			var originalBaseScriptLines = originalBaseScript.Split('\n');
			var originalScriptToPatchLines = originalScriptToPatch.Split('\n');
			var localBaseScriptLines = localBaseScript.Split('\n');

			// do the three-way merge
			var result = Diff.Diff3Merge(localBaseScriptLines, originalBaseScriptLines, originalScriptToPatchLines, true);
			var patchedScriptLines = new List<string>();
			var failed = 0;
			var succeeded = 0;
			foreach (var okBlock in result.Select(block => block as Diff.MergeOkResultBlock)) {
				if (okBlock != null) {
					succeeded++;
					patchedScriptLines.AddRange(okBlock.ContentLines);
				} else {
					failed++;
				}
			}
			if (failed > 0) {
				_logger.Warn("Merge failed ({0} block(s) ok, {1} block(s) conflicted. Needs manual resolving", succeeded, failed);
				return;
			}
			var patchedScript = string.Join("\n", patchedScriptLines);
			_logger.Info("Successfully merged changes - {0} block(s) applied.", succeeded);

			// save script to table
			try {
				_visualPinballManager.SetTableScript(localFilePathToPatch, patchedScript);
			} catch (Exception e) {
				_logger.Error(e, "Error writing patched script back to table file.");
				return;
			}
			game.PatchedTableScript = patchedScript;

			_logger.Info("Successfully wrote back script to table file.");*/
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