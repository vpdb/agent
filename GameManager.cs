using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
		public ObservableCollection<Platform> Platforms { get; set; } = new ObservableCollection<Platform>();

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
				foreach (PinballXSystem system in systems) {
					logger.Debug("Retrieving vpdb.json at {0}", system.DatabasePath);
					syncPlatform(new Platform(system));
				}
			});
		}

		/// <summary>
		/// Returns all games of all platforms
		/// </summary>
		/// <returns></returns>
		public List<Game> GetGames()
		{
			List<Game> games = new List<Game>();
			foreach (Platform platform in Platforms) {
				games.AddRange(platform.Games);
			}
			return games;
		}

		private Platform syncPlatform(Platform platform)
		{
			string vpdbJson = platform.DatabasePath + @"\vpdb.json";

			Platform parsedPlatform = parsePlatform(vpdbJson);

			if (parsedPlatform == null) {
				logger.Warn("No vpdb.json at {0}", vpdbJson);
				platform.Games = mergeGames(menuManager.GetGames(platform.DatabasePath), null, platform.TablePath);
			} else {
				logger.Info("Found and parsed vpdb.json at {0}", vpdbJson);
				platform.Games = mergeGames(menuManager.GetGames(platform.DatabasePath), parsedPlatform.Games, platform.TablePath);
			}
			logger.Trace("Merged {0} games", platform.Games.Count);

			saveJson(platform, vpdbJson);

			Platform existingPlatform = Platforms.FirstOrDefault(p => { return p.Name.Equals(platform.Name); });
			if (existingPlatform != null) {
				Platforms.Remove(existingPlatform);
			}
			Platforms.Add(platform);
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
