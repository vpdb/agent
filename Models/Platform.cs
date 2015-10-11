using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Network;

namespace VpdbAgent.Models
{
	public class Platform : ReactiveObject
	{

		private readonly Logger _logger = Locator.CurrentMutable.GetService<Logger>();

		/// <summary>
		/// Name of the platform. Serves as ID.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// True if enabled in PinballX.ini, False otherwise.
		/// </summary>
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// True if selected in UI, False otherwise.
		/// </summary>
		public bool IsSelected { get; set; } = true;

		/// <summary>
		/// The working path of the executable when launched
		/// </summary>
		public string WorkingPath { get; set; }

		/// <summary>
		/// Folder of the platform's table files
		/// </summary>
		public string TablePath { get; set; }

		/// <summary>
		/// File name of the executable
		/// </summary>
		public string Executable { get; set; }

		/// <summary>
		/// Parameters for the executable to play the table.
		/// </summary>
		/// <example>
		/// /play -"[TABLEPATH]\[TABLEFILE]"
		/// </example>
		public string Parameters { get; set; }

		/// <summary>
		/// Platform type. 
		/// </summary>
		public PlatformType Type { get; set; }

		/// <summary>
		/// Absolute path to database folder.
		/// </summary>
		public string DatabasePath { get; set; }

		/// <summary>
		/// Absolute path to media folder.
		/// </summary>
		public string MediaPath { get; set; }

		/// <summary>
		/// Absolute path to our internal database JSON file.
		/// </summary>
		public string DatabaseFile => DatabasePath + @"\vpdb.json";

		public readonly ReactiveList<Game> Games = new ReactiveList<Game>();

		public Platform(PinballXSystem system)
		{
			Name = system.Name;
			IsEnabled = system.Enabled;
			WorkingPath = system.WorkingPath;
			TablePath = system.TablePath;
			Executable = system.Executable;
			Parameters = system.Parameters;
			Type = system.Type;
			DatabasePath = system.DatabasePath;
			MediaPath = system.MediaPath;

			UpdateGames(system);
		}

		private void UpdateGames(PinballXSystem system)
		{
			var games = MergeGames(system);
			Application.Current.Dispatcher.Invoke((Action)delegate {
				using (Games.SuppressChangeNotifications()) {
					// todo make this more intelligent by diff'ing and changing instead of drop-and-create
					Games.Clear();
					Games.AddRange(games);
				}
			});
		}

		/// <summary>
		/// Takes games parsed from the XML database of a system and merges
		/// them with the local .json database (and saves the result back to
		/// the .json).
		/// </summary>
		/// <param name="system">System in which the game changed</param>
		private IEnumerable<Game> MergeGames(PinballXSystem system)
		{
			var db = UnmarshalDatabase();
			var xmlGames = system.Games;
			List<Game> mergedGames;
			if (db == null) {
				_logger.Warn("No vpdb.json at {0}", DatabaseFile);
				mergedGames = MergeGames(xmlGames, null, TablePath);
			} else {
				_logger.Info("Found and parsed vpdb.json at {0}", DatabaseFile);
				mergedGames = MergeGames(xmlGames, db.Games, TablePath);
			}
			// TODO don't recreate the database, because at some point it'll contain more than just the games.
			MarshallDatabase(new Database(mergedGames));

			return mergedGames;
		}

		/// <summary>
		/// Merges a list of games parsed from an .XML file with a list of 
		/// games read from the internal .json database file
		/// </summary>
		/// <param name="xmlGames">Games read from an .XML file</param>
		/// <param name="jsonGames">Games read from the internal .json database</param>
		/// <param name="tablePath">Path to the table folder</param>
		/// <returns>List of merged games</returns>
		private List<Game> MergeGames(IEnumerable<PinballX.Models.Game> xmlGames, IEnumerable<Game> jsonGames, string tablePath)
		{
			var games = new List<Game>();
			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var xmlGame in xmlGames) {
				var jsonGame = jsonGames?.FirstOrDefault(g => (g.Id.Equals(xmlGame.Description)));
				games.Add(jsonGame == null
					? new Game(xmlGame, tablePath, this)
					: jsonGame.Merge(xmlGame, tablePath, this)
				);
			}
			return games;
		}

		/// <summary>
		/// Reads the internal .json file of a given platform and returns the 
		/// unmarshaled menu object.
		/// </summary>
		/// <returns>Deserialized object</returns>
		private Database UnmarshalDatabase()
		{
			if (!System.IO.File.Exists(DatabaseFile)) {
				return null;
			}

			var serializer = new JsonSerializer {
				NullValueHandling = NullValueHandling.Ignore,
				ContractResolver = new SnakeCasePropertyNamesContractResolver(),
				Formatting = Formatting.Indented
			};

			using (var sr = new StreamReader(DatabaseFile))
			using (JsonReader reader = new JsonTextReader(sr)) {
				try {
					var db = serializer.Deserialize<Database>(reader);
					reader.Close();
					return db;
				} catch (Exception e) {
					_logger.Error(e, "Error parsing vpdb.json, deleting and ignoring.");
					reader.Close();
					System.IO.File.Delete(DatabaseFile);
					return null;
				}
			}
		}

		/// <summary>
		/// Writes the database to the internal .json file for a given platform.
		/// </summary>
		/// <param name="database">Database to marshal</param>
		private void MarshallDatabase(Database database)
		{
			if (DatabaseFile != null && !Directory.Exists(Path.GetDirectoryName(DatabaseFile))) {
				_logger.Warn("Directory {0} does not exist, not writing vpdb.json.", Path.GetDirectoryName(DatabaseFile));
				return;
			}

			var serializer = new JsonSerializer {
				NullValueHandling = NullValueHandling.Ignore,
				ContractResolver = new SnakeCasePropertyNamesContractResolver(),
				Formatting = Formatting.Indented
			};

			using (var sw = new StreamWriter(DatabaseFile))
			using (JsonWriter writer = new JsonTextWriter(sw)) {
				serializer.Serialize(writer, database);
			}
			_logger.Debug("Wrote vpdb.json back to {0}", DatabaseFile);
		}


		public enum PlatformType
		{
			VP, FP, Custom
		}
	}
}
