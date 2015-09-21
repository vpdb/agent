using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using VpdbAgent.Common;
using VpdbAgent.Models;
using VpdbAgent.PinballX;
using VpdbAgent.Vpdb.Network;

namespace VpdbAgent
{
	/// <summary>
	/// Our internal game API.
	/// 
	/// It manages the games that are defined in the user's XML database of
	/// PinballX. However, since we're dealing with more data than what's in 
	/// XMLs, we keep our own data structure in a JSON file.
	/// 
	/// JSON files are system-specific, meaning for every system defined in
	/// PinballX (we call them "Platforms"), there is one JSON file. Games
	/// are always linked to the respective system so we know where to retrieve
	/// table files, media etc.
	/// 
	/// This is how the JSON files are generated:
	/// 
	///  1. GameManager instantiates MenuManager which parses PinballX.ini
	///  2. GameManager loops through parsed systems and retrieves local vpdb.jsons
	///  3. GameManager merges games from MenuManager and vpdb.json to new vpdb.jsons
	///  4. GameManager dumps new vpdb.jsons
	/// 
	/// Everything is event-based, since we want to automatically repeat the 
	/// process when relevant files change. That means, for retrieving the 
	/// systems mentioned above, we subscribe to the SystemsChangedHandler.
	/// 
	/// </summary>
	public class GameManager
	{
		private static GameManager INSTANCE;
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		private MenuManager menuManager = MenuManager.GetInstance();
		public ObservableCollection<Platform> Platforms { get; private set; } = new ObservableCollection<Platform>();

		/// <summary>
		/// Private constructor
		/// </summary>
		/// <see cref="GetInstance"/>
		private GameManager()
		{
			menuManager.SystemsChanged += new MenuManager.SystemsChangedHandler(OnPlatformsChanged);
		}


		/// <summary>
		/// Triggers data update
		/// </summary>
		/// <returns>This instance</returns>
		public GameManager Initialize()
		{
			menuManager.Initialize();
			return this;
		}

		/// <summary>
		/// Platforms have changed or are being initialized.
		/// 
		/// Triggered at startup and when PinballX.ini changes.
		/// </summary>
		/// <param name="systems">Parsed list of systems (platforms)</param>
		private void OnPlatformsChanged(List<PinballXSystem> systems)
		{
			// run on UI thread
			App.Current.Dispatcher.Invoke((Action)delegate {
				Platforms.Clear();
				foreach (PinballXSystem system in systems) {
					logger.Debug("Retrieving vpdb.json at {0}", system.DatabasePath);
					Platforms.Add(syncPlatform(new Platform(system)));
				}
			});
		}

		/// <summary>
		/// Returns all games of currently selected platforms
		/// 
		/// This is terribly ineffecient since on every game add, all listeners
		/// are notified (times number of platforms). Probably should roll our 
		/// own publisher. 
		/// @FIXME
		/// </summary>
		/// <returns></returns>
		public LazyObservableCollection<Game> GetGames(ICollectionView platforms)
		{
			LazyObservableCollection<Game> games = new LazyObservableCollection<Game>();

			// update data on when platforms change
			platforms.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler((a, b) =>
			{
				ListCollectionView list = a as ListCollectionView;
				NotifyCollectionChangedEventArgs args = b as NotifyCollectionChangedEventArgs;
				games.Clear();
				foreach (Platform platform in list) {
					foreach (Game game in platform.Games) {
						games.Add(game);
					}
				}
				games.NotifyRepopulated();
			});

			// initial data
			foreach (Platform platform in platforms) {
				foreach (Game game in platform.Games) {
					games.Add(game);
				}
			}
			return games;
		}

		private Platform syncPlatform(Platform platform)
		{
			string vpdbJson = platform.DatabasePath + @"\vpdb.json";

			Platform parsedPlatform = parsePlatform(vpdbJson);

			List<PinballX.Models.Game> xmlGames = menuManager.GetGames(platform.DatabasePath);
			if (parsedPlatform == null) {
				logger.Warn("No vpdb.json at {0}", vpdbJson);
				platform.Games = mergeGames(xmlGames, null, platform.TablePath);
			} else {
				logger.Info("Found and parsed vpdb.json at {0}", vpdbJson);
				platform.Games = mergeGames(xmlGames, parsedPlatform.Games, platform.TablePath);
			}
			logger.Trace("Merged {0} games ({1} from XMLs, {2} from vpdb.json)", platform.Games.Count, xmlGames.Count, parsedPlatform != null ? parsedPlatform.Games.Count : 0);

			saveJson(platform, vpdbJson);
			return platform;
		}

		private Platform parsePlatform(string vpdbJson)
		{
			if (File.Exists(vpdbJson)) {
				JsonSerializer serializer = new JsonSerializer();
				serializer.NullValueHandling = NullValueHandling.Ignore;
				serializer.ContractResolver = new SnakeCasePropertyNamesContractResolver();
				serializer.Formatting = Formatting.Indented;

				using (StreamReader sr = new StreamReader(vpdbJson))
				using (JsonReader reader = new JsonTextReader(sr)) {
					return serializer.Deserialize<Platform>(reader);
				}
			}
			return null;
		}

		private List<Game> mergeGames(List<PinballX.Models.Game> xmlGames, List<Game> jsonGames, string tablePath)
		{
			List<Game> games = new List<Game>();
			foreach (PinballX.Models.Game xmlGame in xmlGames) {
				Game jsonGame = jsonGames == null ? null : jsonGames.FirstOrDefault(g => (g.Id.Equals(xmlGame.Description)));
				if (jsonGame == null) {
					games.Add(new Game(xmlGame, tablePath));
				} else {
					games.Add(jsonGame.merge(xmlGame, tablePath));
				}
			}
			return games;
		}

		private void saveJson(Platform platform, string vpdbJson)
		{
			if (!Directory.Exists(Path.GetDirectoryName(vpdbJson))) {
				logger.Warn("Directory {0} does not exist, not writing vpdb.json.", Path.GetDirectoryName(vpdbJson));
				return;
			}

			JsonSerializer serializer = new JsonSerializer();
			serializer.NullValueHandling = NullValueHandling.Ignore;
			serializer.ContractResolver = new SnakeCasePropertyNamesContractResolver();
			serializer.Formatting = Formatting.Indented;

			using (StreamWriter sw = new StreamWriter(vpdbJson))
			using (JsonWriter writer = new JsonTextWriter(sw)) {
				serializer.Serialize(writer, platform);
			}
			logger.Debug("Wrote vpdb.json back to {0}", vpdbJson);
		}

		public static GameManager GetInstance()
		{
			if (INSTANCE == null) {
				INSTANCE = new GameManager();
			}
			return INSTANCE;
		}
	}
}
