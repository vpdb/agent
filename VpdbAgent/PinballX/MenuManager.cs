using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
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
		//IObservable<Unit> Initialized { get; }

		IObservable<Tuple<PinballXSystem, string, List<PinballXGame>>> GamesUpdated { get; }

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
		public IObservable<Tuple<PinballXSystem, string, List<PinballXGame>>> GamesUpdated => _gamesUpdated;
		public IObservable<string> TableFileChanged => _tableFileChanged;
		public IObservable<string> TableFileRemoved => _tableFileRemoved;
		
		// privates
		private readonly Subject<Unit> _initialized = new Subject<Unit>();
		private readonly Subject<Tuple<PinballXSystem, string, List<PinballXGame>>> _gamesUpdated = new Subject<Tuple<PinballXSystem, string, List<PinballXGame>>>();
		private readonly Subject<string> _tableFileChanged = new Subject<string>();
		private readonly Subject<string> _tableFileRemoved = new Subject<string>();
		private readonly Dictionary<PinballXSystem, IDisposable> _systemDisposables = new Dictionary<PinballXSystem, IDisposable>();
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
	
		public IMenuManager Initialize()
		{
			// todo if we want to support changing pbx folder in the app without restarting, make this dynamic.
			var iniPath = _settingsManager.Settings.PbxFolder + @"\Config\PinballX.ini";

			Systems.ShouldReset.Subscribe(_ => _logger.Info("Systems Items Should Reset."));
			Systems.ItemsAdded.Subscribe(s => _logger.Info("Systems Item {0} Added.", s.Name));
			Systems.ItemsRemoved.Subscribe(s => _logger.Info("Systems Item {0} Removed.", s.Name));
			Systems.ItemChanged.Subscribe(e => _logger.Info("Systems Item Changed: {0}", e.Sender.Name));

			Systems.ShouldReset.ObserveOn(_threadManager.WorkerScheduler).Subscribe(_ => ResetSystems());
			Systems.ItemsAdded.Subscribe(AddSystem);
			Systems.ItemsRemoved.Subscribe(RemoveSystem);

			// in the beginning when there are no systems, we'll get ShouldReset, so update all.
			//Systems.ShouldReset
			//	.ObserveOn(_threadManager.WorkerScheduler)
			//	.Subscribe(UpdateGames);

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

		/// <summary>
		/// Updates all systems.
		/// 
		/// Parses .ini file and subscribes to parsed systems.
		/// </summary>
		/// <remarks>
		/// Triggered at startup and when <c>PinballX.ini</c> changes.
		/// 
		/// Only updates changed systems.
		/// </remarks>
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
		/// Clears all current system subscriptions and resubscribes to new systems.
		/// </summary>
		private void ResetSystems()
		{
			_logger.Info("Resetting all systems...");
			_systemDisposables.Keys.ToList().ForEach(RemoveSystem);
			Systems.ToList().ForEach(AddSystem);
		}

		/// <summary>
		/// Subscribes to a given system if not already subscribed and kicks 
		/// off <see cref="GamesUpdated"/>.
		/// </summary>
		/// <param name="system">System to kick-off and subscribe</param>
		private void AddSystem(PinballXSystem system)
		{
			// skip if already subscribed
			if (_systemDisposables.ContainsKey(system)) {
				return;
			}
			// kick off current games
			system.Games.Keys.ToList().ForEach(databaseFile => {
				_gamesUpdated.OnNext(new Tuple<PinballXSystem, string, List<PinballXGame>>(system, databaseFile, system.Games[databaseFile]));
			});
			// subscribe to future changes
			_systemDisposables.Add(system, system.GamesUpdated.Subscribe(x => _gamesUpdated.OnNext(new Tuple<PinballXSystem, string, List<PinballXGame>>(system, x.Item1, x.Item2))));
		}

		/// <summary>
		/// Unsubscribes from a system and announce to <see cref="GamesUpdated"/>.
		/// </summary>
		/// <param name="system">System to unsubscribe from</param>
		private void RemoveSystem(PinballXSystem system)
		{
			_logger.Info("Removing system \"{0}\".", system.Name);
			
			// announce system removal
			system.Games.Keys.ToList().ForEach(databaseFile => _gamesUpdated.OnNext(new Tuple<PinballXSystem, string, List<PinballXGame>>(system, databaseFile, new List<PinballXGame>())));

			// unsubscribe
			_systemDisposables[system]?.Dispose();
			_systemDisposables.Remove(system);
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
					systems.Add(new PinballXSystem(PlatformType.VP, data["VisualPinball"], _settingsManager, _marshallManager, _logger, _dir));
				}
				if (data["FuturePinball"] != null) {
					systems.Add(new PinballXSystem(PlatformType.FP, data["FuturePinball"], _settingsManager, _marshallManager, _logger, _dir));
				}
				
				for (var i = 0; i < 20; i++) {
					var systemName = "System_" + i;
					if (data[systemName] != null && data[systemName].Count > 0) {
						systems.Add(new PinballXSystem(data[systemName], _settingsManager, _marshallManager, _logger, _dir));
					}
				}
			}
			_logger.Info("Done, {0} systems parsed.", systems.Count);

			return systems;
		}
	}
}
