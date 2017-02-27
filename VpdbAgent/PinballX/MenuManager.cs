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
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Xml;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.Data.Objects;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Download;
using Platform = VpdbAgent.Models.Platform;

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
	/// read/write maintenance of the user's configuration. See <see cref="GameManager"/> for
	/// data structures that are actually used in the application.
	/// </remarks>
	public interface IMenuManager
	{
		/// <summary>
		/// Starts watching file system for configuration changes and triggers an
		/// initial update.
		/// </summary>
		/// <returns>This instance</returns>
		IMenuManager Initialize(ReactiveList<AggregatedGame> games);

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
		IMenuManager UpdateGame(string oldFileName, Game game);

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
		// publics
		public ReactiveList<PinballXSystem> Systems { get; } = new ReactiveList<PinballXSystem>();
		public IObservable<Unit> Initialized => _initialized;
		public IObservable<string> TableFileChanged => _tableFileChanged;
		public IObservable<string> TableFileRemoved => _tableFileRemoved;

		// privates
		private readonly Subject<Unit> _initialized = new Subject<Unit>();
		private readonly Subject<string> _tableFileChanged = new Subject<string>();
		private readonly Subject<string> _tableFileRemoved = new Subject<string>();
		private ReactiveList<AggregatedGame> _games;
		private bool _isInitialized;

		// dependencies
		private readonly IFileSystemWatcher _watcher;
		private readonly ISettingsManager _settingsManager;
		private readonly IMarshallManager _marshallManager;
		private readonly IThreadManager _threadManager;
		private readonly IFile _file;
		private readonly IDirectory _dir;
		private readonly ILogger _logger;

		public MenuManager(IFileSystemWatcher fileSystemWatcher, ISettingsManager settingsManager,
			IMarshallManager marshallManager, IThreadManager threadManager,
			IFile file, IDirectory dir, ILogger logger)
		{
			_watcher = fileSystemWatcher;
			_settingsManager = settingsManager;
			_marshallManager = marshallManager;
			_threadManager = threadManager;
			_file = file;
			_dir = dir;
			_logger = logger;
		}
	
		public IMenuManager Initialize(ReactiveList<AggregatedGame> games)
		{
			// todo if we want to support changing pxb folder in the app without restarting, make this dynamic.
			var iniPath = _settingsManager.Settings.PbxFolder + @"\Config\PinballX.ini";
			var dbPath = _settingsManager.Settings.PbxFolder + @"\Databases\";

			_games = games;

			Systems.ItemsAdded.Subscribe(s => _logger.Info("Systems Items {0} Added.", s.Name));
			Systems.ItemsRemoved.Subscribe(s => _logger.Info("Systems Item {0} Removed.", s.Name));
			Systems.ShouldReset.Subscribe(_ => _logger.Info("Systems Items Should Reset."));
			Systems.ItemChanged.Subscribe(e => _logger.Info("Systems Item Changed: {0}", e.Sender.Name));

			Systems.ShouldReset
				.ObserveOn(_threadManager.WorkerScheduler)
				.Subscribe(UpdateGames);

			/*
			// parse games when systems change
			Systems.Changed
				.ObserveOn(_threadManager.WorkerScheduler)
				.Subscribe(UpdateGames);

			// parse games when .xmls change
			IDisposable xmlWatcher = null;
			IDisposable tableWatcher = null;
			Systems.Changed.Subscribe(systems => {

				// database files
				xmlWatcher?.Dispose();
				xmlWatcher = _watcher.DatabaseWatcher(dbPath, Systems)
					.ObserveOn(_threadManager.WorkerScheduler)
					.Select(path => new { path, system = Systems.FirstOrDefault(s => s.DatabasePath.Equals(Path.GetDirectoryName(path))) })
					.Subscribe(x => UpdateGames(x.system, Path.GetFileName(x.path)));

				// table files
				tableWatcher?.Dispose();
				tableWatcher = _watcher.TablesWatcher(Systems)
					.Subscribe(f => {
						if (_file.Exists(f)) {
							_tableFileChanged.OnNext(f);
						} else {
							_tableFileRemoved.OnNext(f);
						}
					});
			});*/

			// update systems when ini changes (also, kick it off now)
			_watcher.FileWatcher(iniPath)
				.StartWith(iniPath)                             // kick-off without waiting for first file change
				.SubscribeOn(_threadManager.WorkerScheduler)    // do work on background thread
				.Subscribe(UpdateSystems);

			return this;
		}

		public PinballXGame AddGame(PinballXGame game, string databasePath)
		{
			// read current xml
			var xmlPath = Path.Combine(databasePath, _settingsManager.Settings.XmlFile[PlatformType.VP] + ".xml"); // todo make platform dynamic

			if (_settingsManager.Settings.ReformatXml || !_file.Exists(xmlPath)) {
				var menu = _marshallManager.UnmarshallXml(xmlPath);

				// add game
				menu.Games.Add(game);

				// save xml
				_marshallManager.MarshallXml(menu, xmlPath);

			} else {

				var xml = _file.ReadAllText(xmlPath);
				var ns = new XmlSerializerNamespaces();
				ns.Add("", "");

				using (var writer = new StringWriter())
				{
					// find out how the xml is indented
					var match = Regex.Match(xml, "[\n\r]([ \t]+)<", RegexOptions.Multiline | RegexOptions.IgnoreCase);
					var indentChars = match.Success ? match.Groups[1].ToString() : "    ";
						
					// find the position where to insert game
					if (xml.IndexOf("</menu", StringComparison.OrdinalIgnoreCase) < 0) {
						xml = "<menu>\n\n</menu>";
					}

					// serialize game as xml
					using (var xw = XmlWriter.Create(writer, new XmlWriterSettings { IndentChars = indentChars, Indent = true })) {

						var serializer = new XmlSerializer(typeof(PinballXGame));
						serializer.Serialize(xw, game, ns);
						var xmlGame = string.Join("\n", writer
							.ToString()
							.Split('\n')
							.Select(line => line.StartsWith("<?xml") ? "" : line)
							.Select(line => indentChars + line)
							.ToList()) + "\n";

						var pos = xml.LastIndexOf("</menu", StringComparison.OrdinalIgnoreCase);

						// insert game
						xml = xml.Substring(0, pos) + xmlGame + xml.Substring(pos);

						// write back to disk
						_file.WriteAllText(xmlPath, xml);

						_logger.Info("Appended game \"{0}\" to {1}", game.Description, xmlPath);
					}
				}
			}
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

		public IMenuManager UpdateGame(string oldFileName, Game jsonGame)
		{
			var xmlPath = Path.Combine(jsonGame.Platform.DatabasePath, jsonGame.DatabaseFile);
			var newFilename = Path.GetFileNameWithoutExtension(jsonGame.Filename);

			if (_settingsManager.Settings.ReformatXml) {

				// read xml
				var menu = _marshallManager.UnmarshallXml(xmlPath);

				// get game
				var game = menu.Games.FirstOrDefault(g => g.Filename == oldFileName);
				if (game == null) {
					throw new InvalidOperationException($"Cannot find game with ID {jsonGame.Id} in {xmlPath}.");
				}

				// update game
				game.Filename = newFilename;

				// save xml
				_marshallManager.MarshallXml(menu, xmlPath);

			} else {

				var xml = _file.ReadAllText(xmlPath);
				xml = xml.Replace($"name=\"{oldFileName}\"", $"name=\"{newFilename}\"");
				_file.WriteAllText(xmlPath, xml);

				_logger.Info("Replaced name \"{0}\" with \"{1}\" in {2}.", oldFileName, newFilename, xmlPath);
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
			_threadManager.MainDispatcher.Invoke(delegate {
				if (Systems.IsEmpty) {
					using (Systems.SuppressChangeNotifications()) {
						Systems.AddRange(systems);
					}
					return;
				}
				var remainingSystems = new HashSet<string>(Systems.Select(s => s.Name));
				foreach (var newSystem in systems) {
					var oldSystem = Systems.FirstOrDefault(s => s.Name == newSystem.Name);
					if (oldSystem == null) {
						Systems.Add(newSystem);
					} else if (!oldSystem.Equals(newSystem)) {
						oldSystem.Update(newSystem);
						remainingSystems.Remove(oldSystem.Name);
					} else {
						remainingSystems.Remove(oldSystem.Name);
					}
				}
				foreach (var removedSystem in remainingSystems) {
					Systems.Remove(Systems.FirstOrDefault(s => s.Name == removedSystem));
				}
			});
		}

		/// <summary>
		/// Updates all games of all systems.
		/// </summary>
		/// <remarks>Triggered when systems change</remarks>
		/// <param name="unit">Useless changed event args from ReactiveList</param>
		private void UpdateGames(Unit unit)
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
		/// 		<item><term> Parse all XML files (or just the one specified) of the system </term></item>
		/// 		<item><term> Go through Global Games and try to match parsed games by description </term></item>
		/// 		<item><term> If found, update data, otherwise add </term></item>
		/// 		<item><term> If not found, remove from Global Games if there aren't any other references</term></item>
		/// </list>
		/// </remarks>
		/// <param name="system">System to update</param>
		/// <param name="databaseFile">Filename without path. If set, only updates games for given XML file.</param>
		private void UpdateGames(PinballXSystem system, string databaseFile = null)
		{
			_logger.Info("Parsing games for {0} - ({1})...", system, databaseFile ?? "all games");
			var games = ParseGames(system, databaseFile);

			if (_games.IsEmpty) {
				using (Systems.SuppressChangeNotifications()) {
					_games.AddRange(games.Select(g => new AggregatedGame(g)));
				}
				return;
			}

			var selectedGames = _games.Where(g => ReferenceEquals(g.PinballXGame?.PinballXSystem, system)).ToList();
			if (databaseFile != null) {
				selectedGames = selectedGames.Where(g => g.PinballXGame.DatabaseFile == databaseFile).ToList();
			}

			var remainingGames = new HashSet<AggregatedGame>(selectedGames);
			foreach (var newGame in games) {
				var oldGame = selectedGames.FirstOrDefault(g => ReferenceEquals(g.PinballXGame?.PinballXSystem, system) && g.PinballXGame?.Description == newGame.Description);
				if (oldGame == null) {
					_games.Add(new AggregatedGame(newGame));
				} else if (!oldGame.PinballXGame.Equals(newGame)) {
					oldGame.PinballXGame.Update(newGame);
					remainingGames.Remove(oldGame);
				} else {
					remainingGames.Remove(oldGame);
				}
			}
			_games.RemoveAll(remainingGames.Where(g => !g.HasLocalFile && !g.HasMapping));
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

			var data = _marshallManager.ParseIni(iniPath);
			if (data != null) {
				if (data["VisualPinball"] != null) {
					systems.Add(new PinballXSystem(PlatformType.VP, data["VisualPinball"], _settingsManager));
				}
				if (data["FuturePinball"] != null) {
					systems.Add(new PinballXSystem(PlatformType.FP, data["FuturePinball"], _settingsManager));
				}
				
				for (var i = 0; i < 20; i++) {
					var systemName = "System_" + i;
					if (data[systemName] != null && data[systemName].Count > 0) {
						systems.Add(new PinballXSystem(data[systemName], _settingsManager));
					}
				}
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
			if (_dir.Exists(system.DatabasePath)) {
				foreach (var filePath in _dir.GetFiles(system.DatabasePath).Where(filePath => ".xml".Equals(Path.GetExtension(filePath), StringComparison.InvariantCultureIgnoreCase)))
				{
					var currentDatabaseFile = Path.GetFileName(filePath);
					// if database file is specified, drop everything else
					if (databaseFile != null && !databaseFile.Equals(currentDatabaseFile)) {
						continue;
					}
					var menu = _marshallManager.UnmarshallXml(filePath);
					menu.Games.ForEach(game => {
						game.PinballXSystem = system;
						game.DatabaseFile = currentDatabaseFile;
					});
					games.AddRange(menu.Games);
					fileCount++;
				}
			}
			_logger.Debug("Parsed {0} games from {1} XML file(s) at {2}.", games.Count, fileCount, system.DatabasePath);

			return games;
		}
	}
}
