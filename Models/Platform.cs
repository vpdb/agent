using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Newtonsoft.Json;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Network;

namespace VpdbAgent.Models
{
	/// <summary>
	/// PinballX's "system". Note that this entity is never serialized
	/// and resides only in memory.
	/// </summary>
	public class Platform : ReactiveObject
	{
		#region Properties
		/// <summary>
		/// Name of the platform. Serves as ID.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// True if enabled in PinballX.ini, False otherwise.
		/// </summary>
		public bool IsEnabled { get; set; }

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
		#endregion

		public readonly ReactiveList<Game> Games = new ReactiveList<Game>();

		private readonly Database _database;
		private readonly Logger _logger = Locator.CurrentMutable.GetService<Logger>();

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

			_database = UnmarshalDatabase();

			UpdateGames(system);
		}

		/// <summary>
		/// Saves the current database to json.
		/// </summary>
		/// <returns></returns>
		public Platform Save()
		{
			MarshallDatabase();
			return this;
		}

		/// <summary>
		/// Updates the games coming from XML files of PinballX.ini
		/// </summary>
		/// <param name="system">System with games attached</param>
		private void UpdateGames(PinballXSystem system)
		{
			_database.Games = MergeGames(system);
			Application.Current.Dispatcher.Invoke(delegate {
				using (Games.SuppressChangeNotifications()) {
					// todo make this more intelligent by diff'ing and changing instead of drop-and-create
					Games.Clear();
					Games.AddRange(_database.Games);
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
			var xmlGames = system.Games;
			List<Game> mergedGames;
			if (_database == null) {
				_logger.Warn("No vpdb.json at {0}", DatabaseFile);
				mergedGames = MergeGames(xmlGames, null, TablePath);
			} else {
				_logger.Info("Found and parsed vpdb.json at {0}", DatabaseFile);
				mergedGames = MergeGames(xmlGames, _database.Games, TablePath);
			}

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
		/// <returns>Deserialized object or empty database if no file exists or parsing error</returns>
		private Database UnmarshalDatabase()
		{
			if (!File.Exists(DatabaseFile)) {
				return new Database();
			}

			var serializer = new JsonSerializer {
				NullValueHandling = NullValueHandling.Ignore,
				ContractResolver = new SnakeCasePropertyNamesContractResolver(),
				Formatting = Formatting.Indented
			};

			try {
				using (var sr = new StreamReader(DatabaseFile))
				using (JsonReader reader = new JsonTextReader(sr)) {
					try {
						var db = serializer.Deserialize<Database>(reader);
						reader.Close();
						return db ?? new Database();
					} catch (Exception e) {
						_logger.Error(e, "Error parsing vpdb.json, deleting and ignoring.");
						reader.Close();
						File.Delete(DatabaseFile);
						return new Database();
					}
				}
			} catch (Exception e) {
				_logger.Error(e, "Error reading vpdb.json, deleting and ignoring.");
				return new Database();
			}
		}

		/// <summary>
		/// Writes the database to the internal .json file for a given platform.
		/// </summary>
		private void MarshallDatabase()
		{
			// don't do anything if no db
			if (_database == null) {
				_logger.Warn("Nothing to marshall, not writing vpdb.json.");
				return;
			}

			// don't do anything for non-existent folder
			var dbFolder = Path.GetDirectoryName(DatabaseFile);
			if (dbFolder != null && DatabaseFile != null && !Directory.Exists(dbFolder)) {
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
				serializer.Serialize(writer, _database);
			}
			_logger.Debug("Wrote vpdb.json back to {0}", DatabaseFile);
		}

		/// <summary>
		/// Different types of platforms ("systems")
		/// </summary>
		public enum PlatformType
		{
			/// <summary>
			/// Visual Pinball
			/// </summary>
			VP,

			/// <summary>
			/// Future Pinball
			/// </summary>
			FP,

			/// <summary>
			/// Anything else
			/// </summary>
			Custom
		}
	}
}
