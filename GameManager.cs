using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;
using NLog;
using PusherClient;
using ReactiveUI;
using VpdbAgent.Common;
using VpdbAgent.Models;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Views;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using VpdbAgent.Vpdb.Network;
using File = System.IO.File;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent
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
		/// Platforms are 1-way mapped to <see cref="IMenuManager.Systems"/>,
		/// meaning that if systems change (e.g. <c>PinballX.ini</c> is 
		/// manually updated), they are updated but not vice versa.
		/// </summary>
		ReactiveList<Platform> Platforms { get; }

		/// <summary>
		/// Games are 2-way mapped to <see cref="Game"/>, where downstream 
		/// changes (e.g. XMLs in PinballX's database folder change) come from
		/// <see cref="PinballX.Models.Game"/> and upstream changes are written
		/// to the .json file sitting in the system's database folder.
		/// </summary>
		ReactiveList<Game> Games { get; }

		/// <summary>
		/// Initializes the menu manager, which basically starts watching
		/// relevant files.
		/// </summary>
		/// <returns>This instance</returns>
		IGameManager Initialize();

		/// <summary>
		/// Links a release from VPDB to a game.
		/// </summary>
		/// <param name="game">Local game to link to</param>
		/// <param name="release">Release from VPDB</param>
		/// <returns>This instance</returns>
		IGameManager LinkRelease(Game game, Release release);
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
		private readonly Logger _logger;

		// props
		public ReactiveList<Platform> Platforms { get; } = new ReactiveList<Platform>();
		public ReactiveList<Game> Games { get; } = new ReactiveList<Game>();

		private string _dbPath;
		private GlobalDatabase _database;

		// final
		private readonly JsonSerializer _serializer = new JsonSerializer {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new SnakeCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};

		public GameManager(IMenuManager menuManager, IVpdbClient vpdbClient, ISettingsManager settingsManager, Logger logger)
		{
			_menuManager = menuManager;
			_vpdbClient = vpdbClient;
			_settingsManager = settingsManager;
			_logger = logger;

			var systems = _menuManager.Systems;

			// populate platforms when system change
			systems.Changed
				.Skip(1)
				.ObserveOn(Scheduler.Default)
				.Subscribe(UpdatePlatforms);

			// populate platform when games change
			systems.Changed
				.ObserveOn(Scheduler.Default)
				.SelectMany(_ => systems
					.Select(system => system.Games.Changed.Select(__ => system))
				.Merge())
			.Subscribe(UpdatePlatform);

			systems.Changed
				.SelectMany(_ => systems
					.Select(system => system.Games.Changed.Select(__ => system))
				.Merge())
			.Subscribe(x =>
			{
				Console.WriteLine(systems);
			});

			// here we push all games in all platforms into the Games list. See http://stackoverflow.com/questions/15254708/
			var whenPlatformsOrGamesInThosePlatformsChange = Observable.Merge(
				Platforms.Changed                                                      // one of the games changes
					.SelectMany(_ => Platforms.Select(x => x.Games.Changed).Merge())
					.Select(_ => Unit.Default),
				Platforms.Changed.Select(_ => Unit.Default));                          // one of the platforms changes

			whenPlatformsOrGamesInThosePlatformsChange.StartWith(Unit.Default)
				.Select(_ => Platforms.SelectMany(x => x.Games).ToList())
				.Where(games => games.Count > 0)
				.Subscribe(games => {
					// TODO better logic
					using (Games.SuppressChangeNotifications()) {
						Games.RemoveRange(0, Games.Count);
						Games.AddRange(games);
					}
					_logger.Info("Set {0} games.", games.Count);
				});

			// update releases from VPDB on the first run, but delay it a bit so it 
			// doesn't do all that shit at the same time!
			Games.Changed.Take(1).Delay(TimeSpan.FromSeconds(2)).Subscribe(_ => UpdateReleases());

			// subscribe to pusher
			vpdbClient.UserChannel.Subscribe(OnChannelJoined);
		}

		public IGameManager Initialize()
		{
			// settings must be initialized before doing this.
			if (!_settingsManager.IsInitialized()) {
				throw new InvalidOperationException("Must initialize settings before game manager.");
			}
			_dbPath = Path.Combine(_settingsManager.PbxFolder, @"Databases\vpdb.json");
			_database = UnmarshallDatabase();
			_logger.Info("Global database with {0} release(s) loaded.", _database.Releases.Count);

			_menuManager.Initialize();
			return this;
		}

		public IGameManager LinkRelease(Game game, Release release)
		{
			AddRelease(release);
			game.ReleaseId = release.Id;
			game.Release = release;
			game.Platform.Save();
			return this;
		}

		private void AddRelease(Release release)
		{
			if (!_database.Releases.ContainsKey(release.Id)) {
				_database.Releases.Add(release.Id, release);
			} else {
				_database.Releases[release.Id] = release;
			}
			MarshallDatabase();
		}

		/// <summary>
		/// Updates platform and games for a given system. <br/>
		/// 
		/// This takes changed data from a system, updates platform and
		/// games, and writes back the result to the json file.
		/// </summary>
		/// <remarks>
		/// Triggered by changes of any of the system's games.
		/// </remarks>
		/// <param name="system"></param>
		private void UpdatePlatform(PinballXSystem system)
		{
			_logger.Info("Updating games for {0}", system);

			// create new platform and find old
			var newPlatform = new Platform(system, _database);
			var oldPlatform = Platforms.FirstOrDefault(p => p.Name.Equals(system.Name));

			// save vpdb.json for updated platform
			newPlatform.Save();
			
			// update platforms back on main thread
			Application.Current.Dispatcher.Invoke(delegate {
				using (Platforms.SuppressChangeNotifications()) {
					if (oldPlatform != null) {
						Platforms.Remove(oldPlatform);
					}
					Platforms.Add(newPlatform);
				}
			});
		}

		/// <summary>
		/// Updates all platforms and games. <br/>
		/// 
		/// This takes all available systems, re-creates platforms
		/// and games, and writes back the results to the json files.
		/// </summary>
		/// <remarks>
		/// Triggered by any system changes.
		/// </remarks>
		/// <param name="args"></param>
		private void UpdatePlatforms(NotifyCollectionChangedEventArgs args)
		{
			_logger.Info("Updating all games for all platforms");

			// create platforms from games
			var platforms = _menuManager.Systems.Select(system => new Platform(system, _database)).ToList();

			// write vpdb.json
			platforms.ForEach(p => p.Save());

			// update platforms back on main thread
			Application.Current.Dispatcher.Invoke(delegate {
				using (Platforms.SuppressChangeNotifications()) {
					// todo make this more intelligent by diff'ing and changing instead of drop-and-create
					Platforms.Clear();
					Platforms.AddRange(platforms);
				}
			});
		}

		/// <summary>
		/// Retrieves all known releases from VPDB and updates them locally.
		/// </summary>
		private void UpdateReleases()
		{
			var releaseIds = Games.Where(g => g.HasRelease).Select(g => g.ReleaseId).ToList();
			if (releaseIds.Count > 0) {
				_logger.Info("Updating {0} release(s)", releaseIds.Count);
				_vpdbClient.Api.GetReleasesByIds(string.Join(",", releaseIds))
					.SubscribeOn(Scheduler.Default)
					.Subscribe(releases => {
						// update releases
						foreach (var release in releases) {
							if (!_database.Releases.ContainsKey(release.Id)) {
								_database.Releases.Add(release.Id, release);
							} else {
								_database.Releases[release.Id].Update(release);
							}
						}
						// save
						MarshallDatabase();
					});
			} else {
				_logger.Info("Skipping release update, no linked releases found.");
			}
		}

		/// <summary>
		/// Executed when the pusher connection with the private user channel
		/// is established and we can subscribe to messages.
		/// </summary>
		/// <param name="userChannel">User channel object</param>
		private void OnChannelJoined(Channel userChannel)
		{
			if (userChannel == null) {
				return;
			}

			// subscribe through a subject so we can do more fun stuff with it
			var star = new Subject<dynamic>();
			var unstar = new Subject<dynamic>();
			userChannel.Bind("star", data => star.OnNext(data));
			userChannel.Bind("unstar", data => unstar.OnNext(data));

			// star
			star.ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
			{
				if ("release".Equals((string)data.type)) {
					var release = _database.Releases[(string)data.id];
					if (release != null) {
						release.Starred = true;
						MarshallDatabase();
						_logger.Info("Toggled star on release {0} [on]", release.Name);
					} else {
						_logger.Info("Ignoring star for id {0}", data.id);
					}
				}
			});

			// unstar
			unstar.ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
			{
				if ("release".Equals((string)data.type)) {
					var release = _database.Releases[(string)data.id];
					if (release != null) {
						release.Starred = false;
						MarshallDatabase();
						_logger.Info("Toggled star on release {0} [off]", release.Name);
					} else {
						_logger.Info("Ignoring star for id {0}", data.id);
					}
				}
			});
		}

		private GlobalDatabase UnmarshallDatabase()
		{
			if (!File.Exists(_dbPath)) {
				_logger.Info("Creating new global database, {0} not found.", _dbPath);
				return new GlobalDatabase();
			}
			try {
				using (var sr = new StreamReader(_dbPath))
				using (JsonReader reader = new JsonTextReader(sr)) {
					try {
						var db = _serializer.Deserialize<GlobalDatabase>(reader);
						reader.Close();
						return db ?? new GlobalDatabase();
					} catch (Exception e) {
						_logger.Error(e, "Error parsing {0}, deleting and ignoring.", _dbPath);
						reader.Close();
						File.Delete(_dbPath);
						return new GlobalDatabase();
					}
				}
			} catch (Exception e) {
				_logger.Error(e, "Error reading {0}, deleting and ignoring.", _dbPath);
				return new GlobalDatabase();
			}
		}

		private void MarshallDatabase()
		{
			var dbFolder = Path.GetDirectoryName(_dbPath);
			if (dbFolder != null && !Directory.Exists(dbFolder)) {
				_logger.Warn("Directory {0} does not exist, not writing vpdb.json.", dbFolder);
				return;
			}

			try {
				using (var sw = new StreamWriter(_dbPath))
				using (JsonWriter writer = new JsonTextWriter(sw)) {
					_serializer.Serialize(writer, _database);
				}
				_logger.Debug("Wrote vpdb.json back to {0}", _dbPath);
			} catch (Exception e) {
				_logger.Error(e, "Error writing database to {0}", _dbPath);
			}
		}
	}
}