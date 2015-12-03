using IniParser;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Xml.Serialization;
using ReactiveUI;
using VpdbAgent.PinballX.Models;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using VpdbAgent.Application;
using VpdbAgent.Vpdb.Download;

namespace VpdbAgent.PinballX
{
	/// <summary>
	/// Manages PinballX's menu structure.
	/// 
	/// When initialized, watches PinballX.ini as well as all .XML files
	/// that are referenced in it for changes.
	/// </summary>
	/// 
	/// <remarks>
	/// Games are stored in the systems objects. This class really only does
	/// read-only maintenance of the user's configuration. See <see cref="GameManager"/> for
	/// data structures that are actually used in the application.
	/// </remarks>
	public interface IMenuManager
	{
		/// <summary>
		/// Starts watching file system for configuration changes and triggers an
		/// initial update.
		/// </summary>
		/// <returns>This instance</returns>
		IMenuManager Initialize();

		/// <summary>
		/// Adds a new game to the PinballX database.
		/// </summary>
		/// <param name="game">Game to add</param>
		/// <param name="databasePath">Full path to the database folder</param>
		/// <returns></returns>
		PinballXGame AddGame(PinballXGame game, string databasePath);

		/// <summary>
		/// Instantiates a new game from a given download job.
		/// </summary>
		/// <param name="job">Download job</param>
		/// <returns></returns>
		PinballXGame NewGame(Job job);

		/// <summary>
		/// Updates a game. If the game is not found, an exception is thrown.
		/// </summary>
		/// <remarks>
		/// If the game originates from our own Vpdb.xml, data is marshalled as
		/// as usual. However, if it comes from another (i.e.: the user's) xml,
		/// we do a string-replace in order to keep the rest of the file as-is.
		/// </remarks>
		/// <param name="oldFileName">File ID ("name") of the game to update</param>
		/// <param name="game">Game to update</param>
		/// <returns>This instance</returns>
		IMenuManager UpdateGame(string oldFileName, VpdbAgent.Models.Game game);

		/// <summary>
		/// Systems parsed from <c>PinballX.ini</c>.
		/// </summary>
		ReactiveList<PinballXSystem> Systems { get; }

		/// <summary>
		/// A one-time messenger that announces that the all systems have been 
		/// parsed and <see cref="Systems"/> is filled up with all available
		/// games.
		/// </summary>
		IObservable<Unit> Initialized { get; }

		/// <summary>
		/// A table file has been changed or added (or renamed to given path).
		/// </summary>
		IObservable<string> TableFileChanged { get; }

		/// <summary>
		/// A table file has been deleted (or renamed from given path).
		/// </summary>
		IObservable<string> TableFileRemoved { get; }
	}

	/// <summary>
	/// Application logic for <see cref="IMenuManager"/>.
	/// </summary>
	public class MenuManager : IMenuManager
	{
		public const string VpdbXml = "Vpdb.xml";

		// publics
		public ReactiveList<PinballXSystem> Systems { get; } = new ReactiveList<PinballXSystem>();
		public IObservable<Unit> Initialized => _initialized;
		public IObservable<string> TableFileChanged => _tableFileChanged;
		public IObservable<string> TableFileRemoved => _tableFileRemoved;

		// privates
		private readonly Subject<Unit> _initialized = new Subject<Unit>();
		private readonly Subject<string> _tableFileChanged = new Subject<string>();
		private readonly Subject<string> _tableFileRemoved = new Subject<string>();
		private bool _isInitialized;

		// dependencies
		private readonly IFileSystemWatcher _watcher;
		private readonly ISettingsManager _settingsManager;
		private readonly CrashManager _crashManager;
		private readonly Logger _logger;

		public MenuManager(IFileSystemWatcher fileSystemWatcher, ISettingsManager settingsManager, 
			CrashManager crashManager, Logger logger)
		{
			_watcher = fileSystemWatcher;
			_settingsManager = settingsManager;
			_crashManager = crashManager;
			_logger = logger;
		}
	
		public IMenuManager Initialize()
		{
			var iniPath = _settingsManager.Settings.PbxFolder + @"\Config\PinballX.ini";
			var dbPath = _settingsManager.Settings.PbxFolder + @"\Databases\";

			// update systems when ini changes (also, kick it off now)
			_watcher.FileWatcher(iniPath)
				.StartWith(iniPath)                // kick-off without waiting for first file change
				.SubscribeOn(Scheduler.Default)    // do work on background thread
				.Subscribe(UpdateSystems);

			// parse games when systems change
			Systems.Changed
				.ObserveOn(Scheduler.Default)
				.Subscribe(UpdateGames);

			// parse games when .xmls change
			IDisposable xmlWatcher = null;
			IDisposable tableWatcher = null;
			Systems.Changed.Subscribe(systems => {

				// database files
				xmlWatcher?.Dispose();
				xmlWatcher = _watcher.DatabaseWatcher(dbPath, Systems)
					.ObserveOn(Scheduler.Default)
					.Select(path => new { path, system = Systems.FirstOrDefault(s => s.DatabasePath.Equals(Path.GetDirectoryName(path))) })
					.Subscribe(x => UpdateGames(x.system, Path.GetFileName(x.path)));

				// table files
				tableWatcher?.Dispose();
				tableWatcher = _watcher.TablesWatcher(Systems)
					.Subscribe(f => {
						if (File.Exists(f)) {
							_tableFileChanged.OnNext(f);
						} else {
							_tableFileRemoved.OnNext(f);
						}
					});
			});
			return this;
		}

		public PinballXGame AddGame(PinballXGame game, string databasePath)
		{
			// read current xml
			var vpdbXml = Path.Combine(databasePath, VpdbXml);
			var menu = UnmarshallXml(vpdbXml);

			// add game
			menu.Games.Add(game);

			// save xml
			MarshallXml(menu, vpdbXml);

			return game;
		}

		public PinballXGame NewGame(Job job)
		{
			return new PinballXGame() {
				Filename = Path.GetFileNameWithoutExtension(job.FilePath),
				Description = job.Release.Game.DisplayName,
				Manufacturer = job.Release.Game.Manufacturer,
				Year = job.Release.Game.Year.ToString()
			};
		}

		public IMenuManager UpdateGame(string oldFileName, VpdbAgent.Models.Game jsonGame)
		{
			if (jsonGame.DatabaseFile.Equals(VpdbXml)) {

				// read xml
				var vpdbXml = Path.Combine(jsonGame.Platform.DatabasePath, VpdbXml);
				var menu = UnmarshallXml(vpdbXml);

				// get game
				var game = menu.Games.FirstOrDefault(g => g.Filename.Equals(oldFileName));
				if (game == null) {
					throw new InvalidOperationException($"Cannot find game with ID {jsonGame.Id} in {vpdbXml}.");
				}

				// update game
				game.Filename = jsonGame.FileId;

				// save xml
				MarshallXml(menu, vpdbXml);

			} else {

				// not our file, string replace.
				var xmlPath = Path.Combine(jsonGame.Platform.DatabasePath, jsonGame.DatabaseFile);

				var xml = File.ReadAllText(xmlPath);
				xml = xml.Replace($"name=\"{oldFileName}\"", $"name=\"{jsonGame.FileId}\"");
				File.WriteAllText(xmlPath, xml);

				_logger.Info("Replaced name \"{0}\" with \"{1}\" in {2}.", oldFileName, jsonGame.FileId, xmlPath);
			}
			return this;
		}

		/// <summary>
		/// Updates all systems.
		/// </summary>
		/// <remarks>Triggered at startup and when <c>PinballX.ini</c> changes.</remarks>
		/// <param name="iniPath">Path to PinballX.ini</param>
		private void UpdateSystems(string iniPath)
		{
			// here, we're on a worker thread.
			var systems = ParseSystems(iniPath);

			// treat result back on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(delegate
			{
				using (Systems.SuppressChangeNotifications()) {
					// todo make this more intelligent by diff'ing and changing instead of drop-and-create
					Systems.Clear();
					Systems.AddRange(systems);
				}
			});
		}

		/// <summary>
		/// Updates all games of all systems.
		/// </summary>
		/// <remarks>Triggered when systems change</remarks>
		/// <param name="args">Useless changed event args from ReactiveList</param>
		private void UpdateGames(NotifyCollectionChangedEventArgs args)
		{
			_logger.Info("Parsing all games for all systems...");
			foreach (var system in Systems) {
				UpdateGames(system);
			}
			_logger.Info("All games parsed.");

			if (!_isInitialized) {
				_initialized.OnNext(Unit.Default);
				_initialized.OnCompleted();
				_isInitialized = true;
			}
		}

		/// <summary>
		/// Updates all games of a given system.
		/// </summary>
		/// <remarks>
		/// Triggered by XML changes. Updating means:
		///  
		/// <list type="number">
		/// 		<item><term> Parsing all XML files of the system </term></item>
		/// 		<item><term> Reading all games in the XML files </term></item>
		/// 		<item><term> Setting <see cref="PinballXSystem.Games">Games</see> of the system </term></item>
		/// </list>
		/// </remarks>
		/// <param name="system">System to update</param>
		/// <param name="databaseFile">Filename without path. If set, only updates games for given XML file.</param>
		private void UpdateGames(PinballXSystem system, string databaseFile = null)
		{
			_logger.Info("Parsing games for {0} - ({1})...", system, databaseFile ?? "all games");
			var games = ParseGames(system, databaseFile);
			using (system.Games.SuppressChangeNotifications()) {
				if (databaseFile == null) {
					system.Games.Clear();
				} else {
					system.Games.RemoveAll(system.Games.Where(game => databaseFile.Equals(game.DatabaseFile)).ToList());
				}
				system.Games.AddRange(games);
			}
		}

		/// <summary>
		/// Parses PinballX.ini and reads all systems from it.
		/// </summary>
		/// <returns>Parsed systems</returns>
		private IEnumerable<PinballXSystem> ParseSystems(string iniPath)
		{
			var systems = new List<PinballXSystem>();
			// only notify after this block
			_logger.Info("Parsing systems from {0}", iniPath);

			if (File.Exists(iniPath)) {
				var parser = new FileIniDataParser();
				var data = parser.ReadFile(iniPath);
				systems.Add(new PinballXSystem(VpdbAgent.Models.Platform.PlatformType.VP, data["VisualPinball"]));
				systems.Add(new PinballXSystem(VpdbAgent.Models.Platform.PlatformType.FP, data["FuturePinball"]));
				for (var i = 0; i < 20; i++) {
					if (data["System_" + i] != null) {
						systems.Add(new PinballXSystem(data["System_" + i]));
					}
				}
			} else {
				_logger.Error("PinballX.ini at {0} does not exist.", iniPath);
			}
			_logger.Info("Done, {0} systems parsed.", systems.Count);

			return systems;
		}

		/// <summary>
		/// Parses all games for a given system.
		/// </summary>
		/// <remarks>
		/// "Parsing" means reading and unmarshalling all XML files in the 
		/// system's database folder.
		/// </remarks>
		/// <param name="system">System to parse games for</param>
		/// <param name="databaseFile">If set, only parse games for given XML file</param>
		/// <returns>Parsed games</returns>
		private IEnumerable<PinballXGame> ParseGames(PinballXSystem system, string databaseFile = null)
		{
			if (system == null) {
				_logger.Warn("Unknown system, not parsing games.");
				return new List<PinballXGame>();
			}
			_logger.Info("Parsing games at {0}", system.DatabasePath);

			var games = new List<PinballXGame>();
			var fileCount = 0;
			if (Directory.Exists(system.DatabasePath)) {
				foreach (var filePath in Directory.GetFiles(system.DatabasePath).Where(filePath => ".xml".Equals(Path.GetExtension(filePath), StringComparison.InvariantCultureIgnoreCase)))
				{
					var currentDatabaseFile = Path.GetFileName(filePath);
					// if database file is specified, drop everything else
					if (databaseFile != null && !databaseFile.Equals(currentDatabaseFile)) {
						continue;
					}
					var menu = UnmarshallXml(filePath);
					menu.Games.ForEach(game => game.DatabaseFile = currentDatabaseFile);
					games.AddRange(menu.Games);
					fileCount++;
				}
			}
			_logger.Debug("Parsed {0} games from {1} XML file(s) at {2}.", games.Count, fileCount, system.DatabasePath);

			return games;
		}

		/// <summary>
		/// Returns an unmarshalled object for a given .XML file
		/// </summary>
		/// <param name="filepath">Absolute path to the .XML file</param>
		/// <returns></returns>
		private PinballXMenu UnmarshallXml(string filepath)
		{
			var menu = new PinballXMenu();

			if (!File.Exists(filepath)) {
				return menu;
			}
			Stream reader = null;
			try {
				var serializer = new XmlSerializer(typeof(PinballXMenu));
				reader = new FileStream(filepath, FileMode.Open);
				menu = serializer.Deserialize(reader) as PinballXMenu;

			} catch (Exception e) {
				_logger.Error(e, "Error parsing {0}: {1}", filepath, e.Message);
				_crashManager.Report(e, "xml");

			} finally {
				reader?.Close();
			}
			return menu;
		}

		/// <summary>
		/// Saves the menu back to the XML file.
		/// </summary>
		/// <remarks>
		/// This should only be used for updating or adding games by VPDB Agent,
		/// i.e. those in Vpdb.xml that is managed by VPDB Agent. For existing games
		/// another serializer should be used that keeps eventual comments and
		/// ordering intact.
		/// </remarks>
		/// <param name="menu"></param>
		/// <param name="filepath"></param>
		private void MarshallXml(PinballXMenu menu, string filepath)
		{
			try {
				var serializer = new XmlSerializer(typeof(PinballXMenu));
				using (TextWriter writer = new StreamWriter(filepath)) {
					serializer.Serialize(writer, menu);
					_logger.Info("Saved {0}.", filepath);
				}
			} catch (Exception e) {
				_logger.Error(e, "Error writing XML to {0}: {1}", filepath, e.Message);
				_crashManager.Report(e, "xml");
			}

		}
	}
}
