using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Newtonsoft.Json;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
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

		public readonly Subject<Unit> GamePropertyChanged = new Subject<Unit>();
		private readonly PlatformDatabase _database;
		private readonly Logger _logger = Locator.CurrentMutable.GetService<Logger>();
		private readonly CrashManager _crashManager = Locator.CurrentMutable.GetService<CrashManager>();

		private readonly JsonSerializer _serializer = new JsonSerializer {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new SnakeCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};

		public Platform(PinballXSystem system, GlobalDatabase db)
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

			_database = UnmarshallDatabase();

			UpdateGames(system, db);

			// save changes, but at most once per second.
			GamePropertyChanged
				.ObserveOn(Scheduler.Default)
				.Sample(TimeSpan.FromSeconds(1))
				.Subscribe(_ => Save());
		}

		/// <summary>
		/// Saves the current database to json.
		/// </summary>
		/// <returns></returns>
		public Platform Save()
		{
			_database.Games = Games;
			MarshallDatabase();
			return this;
		}

		/// <summary>
		/// Updates the games coming from XML files of PinballX.ini
		/// </summary>
		/// <param name="system">System with games attached</param>
		/// <param name="db">Reference to global database</param>
		private void UpdateGames(PinballXSystem system, GlobalDatabase db)
		{
			_database.Games = MergeGames(system, db);
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
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
		/// <param name="db">Reference to global database</param>
		private IEnumerable<Game> MergeGames(PinballXSystem system, GlobalDatabase db)
		{
			var xmlGames = system.Games;
			List<Game> mergedGames;
			if (_database == null) {
				_logger.Warn("No vpdb.json at {0}", DatabaseFile);
				mergedGames = MergeGames(xmlGames, null, db);
			} else {
				_logger.Info("Found and parsed vpdb.json at {0}", DatabaseFile);
				mergedGames = MergeGames(xmlGames, _database.Games, db);
			}

			return mergedGames;
		}

		/// <summary>
		/// Merges a list of games parsed from an .XML file with a list of 
		/// games read from the internal .json database file
		/// </summary>
		/// <param name="xmlGames">Games read from an .XML file</param>
		/// <param name="jsonGames">Games read from the internal .json database</param>
		/// <param name="db">Reference to global database</param>
		/// <returns>List of merged games</returns>
		private List<Game> MergeGames(IEnumerable<PinballXGame> xmlGames, IEnumerable<Game> jsonGames, GlobalDatabase db)
		{
			_logger.Info("MergeGames() START");

			var games = new List<Game>();
			var enumerableGames = jsonGames as Game[] ?? jsonGames.ToArray();
			var enumerableXmlGames = xmlGames as PinballXGame[] ?? xmlGames.ToArray();

			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var xmlGame in enumerableXmlGames) {
				var jsonGame = enumerableGames.FirstOrDefault(g => (g.Id.Equals(xmlGame.Description)));
				games.Add(jsonGame == null
					? new Game(xmlGame, this, db)
					: jsonGame.Merge(xmlGame, this, db)
				);
			}

			_logger.Info("MergeGames() DONE");
			return games;
		}

		/// <summary>
		/// Reads the internal .json file of a given platform and returns the 
		/// unmarshalled database object.
		/// </summary>
		/// <returns>Deserialized object or empty database if no file exists or parsing error</returns>
		private PlatformDatabase UnmarshallDatabase()
		{
			if (!File.Exists(DatabaseFile)) {
				return new PlatformDatabase();
			}

			try {
				using (var sr = new StreamReader(DatabaseFile))
				using (JsonReader reader = new JsonTextReader(sr)) {
					try {
						var db = _serializer.Deserialize<PlatformDatabase>(reader);
						reader.Close();
						return db ?? new PlatformDatabase();
					} catch (Exception e) {
						_logger.Error(e, "Error parsing vpdb.json, deleting and ignoring.");
						_crashManager.Report(e, "json");
						reader.Close();
						File.Delete(DatabaseFile);
						return new PlatformDatabase();
					}
				}
			} catch (Exception e) {
				_logger.Error(e, "Error reading vpdb.json, deleting and ignoring.");
				_crashManager.Report(e, "json");
				return new PlatformDatabase();
			}
		}

		/// <summary>
		/// Writes the database to the internal .json file for a given platform.
		/// </summary>
		private void MarshallDatabase()
		{
			// don't do anything for non-existent folder
			var dbFolder = Path.GetDirectoryName(DatabaseFile);
			if (dbFolder != null && DatabaseFile != null && !Directory.Exists(dbFolder)) {
				_logger.Warn("Directory {0} does not exist, not writing vpdb.json.", dbFolder);
				return;
			}

			try {
				using (var sw = new StreamWriter(DatabaseFile))
				using (JsonWriter writer = new JsonTextWriter(sw)) {
					_serializer.Serialize(writer, _database);
				}
				_logger.Debug("Wrote vpdb.json back to {0}", DatabaseFile);
			} catch (Exception e) {
				_logger.Error(e, "Error writing vpdb.json to {0}", DatabaseFile);
				_crashManager.Report(e, "json");
			}
		}

		public override string ToString()
		{
			return $"[Platform] {Name} ({Games.Count})";
		}

		/// <summary>
		/// Different types of platforms ("systems"), as defined by PinballX.
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
