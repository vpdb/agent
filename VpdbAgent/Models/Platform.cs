using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Data.Objects;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using ILogger = NLog.ILogger;

namespace VpdbAgent.Models
{
	/// <summary>
	/// PinballX's "system". Note that this entity is never serialized
	/// and resides only in memory.
	/// </summary>
	[Obsolete]
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

		// dependencies
		private readonly IDependencyResolver _resolver;
		private readonly IMarshallManager _marshallManager;
		private readonly IThreadManager _threadManager;
		private readonly ILogger _logger;

		/// <summary>
		/// The platform specific database
		/// </summary>
		private readonly PlatformDatabase _database;

		/// <summary>
		/// All attached games
		/// </summary>
		public readonly ReactiveList<Game> Games = new ReactiveList<Game>();
		public readonly Subject<Unit> GamePropertyChanged = new Subject<Unit>();

		public Platform(PinballXSystem system, IDependencyResolver resolver)
		{
			_resolver = resolver;
			_marshallManager = resolver.GetService<IMarshallManager>();
			_threadManager = resolver.GetService<IThreadManager>();
			_logger = resolver.GetService<ILogger>();

			Name = system.Name;
			IsEnabled = system.Enabled;
			WorkingPath = system.WorkingPath;
			TablePath = system.TablePath;
			Executable = system.Executable;
			Parameters = system.Parameters;
			Type = system.Type;
			DatabasePath = system.DatabasePath;
			MediaPath = system.MediaPath;

			_database = _marshallManager.UnmarshallPlatformDatabase(DatabaseFile);

			UpdateGames(system);

			// save changes
			GamePropertyChanged
				.ObserveOn(Scheduler.Default)
				//.Sample(TimeSpan.FromSeconds(1)) // disable for now, causes timing issues when updating a release (xml gets updated, platform re-parsed, json re-read but json is still the old, non-updated one, resulting in the new version not being displayed.)
				.Subscribe(_ => Save());
		}

		/// <summary>
		/// Saves the current database to json.
		/// </summary>
		/// <returns></returns>
		public Platform Save()
		{
			_database.Games = Games;
			_marshallManager.MarshallPlatformDatabase(_database, DatabaseFile);
			return this;
		}

		/// <summary>
		/// Updates the games coming from XML files of PinballX.ini
		/// </summary>
		/// <param name="system">System with games attached</param>
		private void UpdateGames(PinballXSystem system)
		{
			_database.Games = MergeGames(system);
			_threadManager.MainDispatcher.Invoke(delegate {
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
			List<Game> mergedGames = new List<Game>();
			/*if (_database == null) {
				_logger.Warn("No vpdb.json at {0}", DatabaseFile);
				mergedGames = MergeGames(xmlGames, new List<Game>());
			} else {
				_logger.Info("Found and parsed vpdb.json at {0}", DatabaseFile);
				mergedGames = MergeGames(xmlGames, _database.Games);
			}*/

			return mergedGames;
		}

		/// <summary>
		/// Merges a list of games parsed from an .XML file with a list of 
		/// games read from the internal .json database file
		/// </summary>
		/// <param name="xmlGames">Games read from an .XML file</param>
		/// <param name="jsonGames">Games read from the internal .json database</param>
		/// <returns>List of merged games</returns>
		private List<Game> MergeGames(IEnumerable<PinballXGame> xmlGames, IEnumerable<Game> jsonGames)
		{
			_logger.Info("MergeGames() START");

			var games = new List<Game>();
			var enumerableGames = jsonGames as Game[] ?? jsonGames.ToArray();
			var enumerableXmlGames = xmlGames as PinballXGame[] ?? xmlGames.ToArray();

			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var xmlGame in enumerableXmlGames) {
				var jsonGame = enumerableGames.FirstOrDefault(g => (g.Id.Equals(xmlGame.Description)));
				games.Add(jsonGame == null
					? new Game(xmlGame, this, _resolver)
					: jsonGame.Merge(xmlGame, this)
				);
			}

			_logger.Info("MergeGames() DONE");
			return games;
		}

		public override string ToString()
		{
			return $"[Platform] {Name} ({Games.Count})";
		}
	}
}
