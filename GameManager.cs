using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
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
	public class GameManager
	{
		private static GameManager INSTANCE;

		private MenuManager menuManager = MenuManager.GetInstance();
		public ObservableCollection<Platform> Platforms { get; set; } = new ObservableCollection<Platform>();

		/// <summary>
		/// So this is how this works:
		///
		///  1. GameManager instantiates MenuManager which parses PinballX.ini
		///  2. GameManager loops through parsed systems and retrieves local vpdb.jsons
		///  3. GameManager merges games from MenuManager and vpdb.json to new vpdb.jsons
		///  4. GameManager dumps new vpdb.jsons
		/// 
		/// </summary>
		private GameManager()
		{
			if (menuManager.Systems != null) {
				foreach (PinballXSystem system in menuManager.Systems) {
					Console.WriteLine("Retrieving vpdb.json at {0}", system.DatabasePath);
					syncPlatform(new Platform(system));
				}
			}
		}

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
				Console.WriteLine("No vpdb.json at {0}", vpdbJson);
				platform.Games = mergeGames(menuManager.GetGames(platform.DatabasePath), null, platform.TablePath);
			} else {
				Console.WriteLine("Found and parsed vpdb.json at {0}", vpdbJson);
				platform.Games = mergeGames(menuManager.GetGames(platform.DatabasePath), parsedPlatform.Games, platform.TablePath);
			}
			Console.WriteLine("Merged {0} games", platform.Games.Count);

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
				Console.WriteLine("Directory {0} does not exist, not writing vpdb.json.", Path.GetDirectoryName(vpdbJson));
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
			Console.WriteLine("Wrote vpdb.json back to {0}", vpdbJson);
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
