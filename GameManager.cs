using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using Newtonsoft.Json;
using NLog;
using ReactiveUI;
using VpdbAgent.Common;
using VpdbAgent.Models;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Models;
using VpdbAgent.Vpdb.Network;
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
	/// Everything is event-based, since we want to automatically repeat the
	/// process when relevant files change. That means, for retrieving the
	/// systems mentioned above, we subscribe to the SystemsChangedHandler.
	/// </summary>
	public class GameManager
	{
		private static GameManager _instance;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// deps
		private readonly MenuManager _menuManager = MenuManager.GetInstance();

		// props
		public IReactiveDerivedList<Platform> Platforms { get; }
		public ReactiveList<Game> Games { get; } = new ReactiveList<Game>();


		/// <summary>
		///     Private constructor
		/// </summary>
		/// <see cref="GetInstance" />
		private GameManager()
		{
			Platforms = _menuManager.Systems.CreateDerivedCollection(system => new Platform(system));

			// http://stackoverflow.com/questions/15254708/creating-a-reactiveui-derived-collection-with-more-elements-than-the-original
			IObservable<List<PinballX.Models.Game>> games = _menuManager.Systems.Changed
				.Select(_ => _menuManager.Systems.SelectMany(x => x.Games).ToList());

			_menuManager.GamesChanged += OnGamesChanged;
		}
	
		/// <summary>
		///     Triggers data update
		/// </summary>
		/// <returns>This instance</returns>
		public GameManager Initialize()
		{
			_menuManager.Initialize();
			return this;
		}

		public void LinkRelease(Game game, Release release)
		{
			game.Release = release;

			WriteDatabase(new Database(GetGames(game.Platform)), game.Platform);
		}

		public IEnumerable<Game> GetGames(Platform platform) {
			return Games.Where(game => game.Platform.Name.Equals(platform.Name));
		}


		/// <summary>
		///     Platforms have changed or are being initialized.
		///     Triggered at startup and when PinballX.ini changes.
		/// </summary>
		/// <param name="args">What changed</param>
		/*
		private void OnPlatformsChanged(NotifyCollectionChangedEventArgs args)
		{
			Logger.Info("Systems changed, updating platforms.");

			// run on ui thread
			Application.Current.Dispatcher.Invoke(delegate {
				Platforms.Clear();
				Games.Clear();
				foreach (var system in _menuManager.Systems) {
					Platforms.Add(new Platform(system));
				}
				Platforms.NotifyRepopulated();
			});
		}*/

		private void OnGamesChanged(PinballXSystem system)
		{
			// retrieve platform based on name
			var platform = Platforms.FirstOrDefault(p => p.Name.Equals(system.Name));
			if (platform == null) {
				Logger.Error("Unknown platform {0}, ignoring game changes.", system.Name);
				return;
			}

			var db = ReadDatabase(platform);

			var xmlGames = system.Games;
			List<Game> mergedGames;
			if (db == null) {
				Logger.Warn("No vpdb.json at {0}", platform.DatabaseFile);
				mergedGames = MergeGames(xmlGames, null, platform.TablePath, platform);
			} else {
				Logger.Info("Found and parsed vpdb.json at {0}", platform.DatabaseFile);
				mergedGames = MergeGames(xmlGames, db.Games, platform.TablePath, platform);
			}
			WriteDatabase(new Database(mergedGames), platform);

			Application.Current.Dispatcher.Invoke(delegate {
				using (Games.SuppressChangeNotifications()) {

					var itemsToRemove = Games.Where(game => game.Platform.Name.Equals(platform.Name)).ToArray();
					foreach (var item in itemsToRemove) {
						Games.Remove(item);
					}
					Games.AddRange(mergedGames);
					Games.Sort();
				};
				Logger.Trace("Merged {0} games ({1} from XMLs)", mergedGames.Count, xmlGames.Count);
			});
		}

		private static List<Game> MergeGames(IEnumerable<PinballX.Models.Game> xmlGames, IEnumerable<Game> jsonGames, string tablePath, Platform platform)
		{
			var games = new List<Game>();
			foreach (var xmlGame in xmlGames) {
				var jsonGame = jsonGames?.FirstOrDefault(g => (g.Id.Equals(xmlGame.Description)));
				games.Add(jsonGame == null
					? new Game(xmlGame, tablePath, platform)
					: jsonGame.Merge(xmlGame, tablePath, platform)
					);
			}
			return games;
		}

		/// <summary>
		///     De-serializes vpdb.json into an object model.
		/// </summary>
		/// <param name="vpdbJson">Path to vpdb.json</param>
		/// <returns>Deserialized object</returns>
		private static Database ReadDatabase(Platform platform)
		{
			if (!System.IO.File.Exists(platform.DatabaseFile)) {
				return null;
			}

			var serializer = new JsonSerializer {
				NullValueHandling = NullValueHandling.Ignore,
				ContractResolver = new SnakeCasePropertyNamesContractResolver(),
				Formatting = Formatting.Indented
			};

			using (var sr = new StreamReader(platform.DatabaseFile))
			using (JsonReader reader = new JsonTextReader(sr)) {
				try {
					var db = serializer.Deserialize<Database>(reader);
					reader.Close();
					return db;
				} catch (Exception e) {
					Logger.Error(e, "Error parsing vpdb.json, deleting and ignoring.");
					reader.Close();
					System.IO.File.Delete(platform.DatabaseFile);
					return null;
				}
			}
		}


		private static void WriteDatabase(Database database, Platform platform)
		{
			if (platform.DatabaseFile != null && !Directory.Exists(Path.GetDirectoryName(platform.DatabaseFile))) {
				Logger.Warn("Directory {0} does not exist, not writing vpdb.json.", Path.GetDirectoryName(platform.DatabaseFile));
				return;
			}

			var serializer = new JsonSerializer {
				NullValueHandling = NullValueHandling.Ignore,
				ContractResolver = new SnakeCasePropertyNamesContractResolver(),
				Formatting = Formatting.Indented
			};

			using (var sw = new StreamWriter(platform.DatabaseFile))
			using (JsonWriter writer = new JsonTextWriter(sw)) {
				serializer.Serialize(writer, database);
			}
			Logger.Debug("Wrote vpdb.json back to {0}", platform.DatabaseFile);
		}

		public static GameManager GetInstance()
		{
			return _instance ?? (_instance = new GameManager());
		}
	}
}