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
using VpdbAgent.Data;
using VpdbAgent.Vpdb.Download;
using ILogger = NLog.ILogger;


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
		/// 
		/// <returns>This instance</returns>
		IMenuManager Initialize();

		/// <summary>
		/// Adds a new game to the PinballX database.
		/// </summary>
		/// <param name="game">Game to add</param>
		/// <returns></returns>
		PinballXGame AddGame(PinballXGame game);

		/// <summary>
		/// Removes a game from the PinballX database.
		/// </summary>
		/// <param name="game">Game to remove</param>
		void RemoveGame(PinballXGame game);

		/// <summary>
		/// Instantiates a new game from a given download job.
		/// </summary>
		/// 
		/// <param name="job">Download job</param>
		/// <returns></returns>
		PinballXGame NewGame(Job job);

		/// <summary>
		/// Renames the game (i.e. <see cref="AggregatedGame.FileName"/> / Name). 
		/// If the game is not found, an exception is thrown.
		/// </summary>
		/// 
		/// <remarks>
		/// If the game originates from our own Vpdb.xml, data is marshalled as
		/// as usual. However, if it comes from another (i.e.: the user's) xml,
		/// we do a string-replace in order to keep the rest of the file as-is.
		/// </remarks>
		/// <param name="oldFileName">File ID ("name") of the game to update</param>
		/// <param name="game">Game to update</param>
		/// <returns>This instance</returns>
		IMenuManager RenameGame(string oldFileName, AggregatedGame game);

		/// <summary>
		/// Systems parsed from <c>PinballX.ini</c>.
		/// </summary>
		ReactiveList<PinballXSystem> Systems { get; }

		/// <summary>
		/// Produces a value every time any data in any database file changes,
		/// or if database files are removed, added or renamed.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that the returned the list of games is always exhaustive, i.e. games 
		/// not in that list for the given database file are to be removed. For 
		/// example, when a database file is deleted, this will produce a Tuple with 
		/// the deleted database filename and an empty list.
		/// </remarks>
		IObservable<Tuple<PinballXSystem, string, List<PinballXGame>>> GamesUpdated { get; }

		/// <summary>
		/// Produces a value every time the mappings for a given system is updated,
		/// created or deleted.
		/// </summary>
		/// 
		/// <remarks>
		/// The returned list of mappings is exhaustive, i.e. mappings not in that
		/// list for the given systems are to be removed. Therefore, when a mapping
		/// file is removed, this triggers with an empty list.
		/// </remarks>
		IObservable<Tuple<PinballXSystem, List<Mapping>>> MappingsUpdated { get; }

		/// <summary>
		/// A table file has been edited.
		/// </summary>
		/// 
		IObservable<string> TableFileCreated { get; }

		/// <summary>
		/// A table file has been updated.
		/// </summary>
		IObservable<string> TableFileChanged { get; }

		/// <summary>
		/// A table file has been renamed.
		/// </summary>
		IObservable<Tuple<string, string>> TableFileRenamed { get; }

		/// <summary>
		/// A table file has been deleted.
		/// </summary>
		IObservable<string> TableFileDeleted { get; }

		/// <summary>
		/// A table folder has been added.
		/// </summary>
		/// 
		/// <remarks>
		/// Usually happens when a new system is added. However, if the system's
		/// table folder is already watched because it's the same as for an already
		/// existing system, this won't fire.
		/// </remarks>
		IObservable<string> TableFolderAdded { get; }

		/// <summary>
		/// A table folder has been removed.
		/// </summary>
		IObservable<string> TableFolderRemoved { get; }

		/// <summary>
		/// Returns a list of all vpt/vpx files in a given folder.
		/// </summary>
		/// TODO support other systems than VP
		/// 
		/// <param name="path">Path to table folder</param>
		/// <returns>List of absolute file paths to table files</returns>
		List<string> GetTableFiles(string path);
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
		public IObservable<Tuple<PinballXSystem, List<Mapping>>> MappingsUpdated => _mappingsUpdated;

		public IObservable<string> TableFileCreated => _watcher.TableFileCreated;
		public IObservable<string> TableFileChanged => _watcher.TableFileChanged;
		public IObservable<Tuple<string, string>> TableFileRenamed => _watcher.TableFileRenamed;
		public IObservable<string> TableFileDeleted  => _watcher.TableFileDeleted;

		public IObservable<string> TableFolderAdded => _watcher.TableFolderAdded;
		public IObservable<string> TableFolderRemoved => _watcher.TableFolderRemoved;

		// privates
		private readonly Subject<Unit> _initialized = new Subject<Unit>();
		private readonly Subject<Tuple<PinballXSystem, string, List<PinballXGame>>> _gamesUpdated = new Subject<Tuple<PinballXSystem, string, List<PinballXGame>>>();
		private readonly Subject<Tuple<PinballXSystem, List<Mapping>>> _mappingsUpdated = new Subject<Tuple<PinballXSystem, List<Mapping>>>();
		private readonly Dictionary<PinballXSystem, IDisposable> _systemDisposables = new Dictionary<PinballXSystem, IDisposable>();

		// dependencies
		private readonly IFileSystemWatcher _watcher;
		private readonly ISettingsManager _settingsManager;
		private readonly IMarshallManager _marshallManager;
		private readonly IThreadManager _threadManager;
		private readonly IDirectory _dir;
		private readonly IFile _file;
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
			var iniPath = _settingsManager.Settings.PinballXFolder + @"\Config\PinballX.ini";

			// setup game relay
			Systems.ShouldReset.ObserveOn(_threadManager.WorkerScheduler).Subscribe(_ => ResetSystems());
			Systems.ItemsAdded.Subscribe(AddSystem);
			Systems.ItemsRemoved.Subscribe(RemoveSystem);

			// setup table file watcher
			Systems.ShouldReset.ObserveOn(_threadManager.WorkerScheduler).Subscribe(_ => _watcher.WatchTables(Systems));
			Systems.ItemsRemoved.ObserveOn(_threadManager.WorkerScheduler).Subscribe(_ => _watcher.WatchTables(Systems));
		
			// update systems when ini changes (also, kick it off now)
			_watcher.FileWatcher(iniPath)
				.StartWith(iniPath)                             // kick-off without waiting for first file change
				.SubscribeOn(_threadManager.WorkerScheduler)    // do work on background thread
				.Subscribe(UpdateSystems);

			return this;
		}

		public List<string> GetTableFiles(string path)
		{
			return _dir
				.GetFiles(path)
				.Where(filePath => ".vpt".Equals(Path.GetExtension(filePath), StringComparison.InvariantCultureIgnoreCase) || ".vpx".Equals(Path.GetExtension(filePath), StringComparison.InvariantCultureIgnoreCase)) 
				.ToList();
		}

		public PinballXGame AddGame(PinballXGame game)
		{
			// read current xml
			var xmlPath = Path.Combine(game.System.DatabasePath, _settingsManager.Settings.XmlFile[game.System.Type] + ".xml");
			_logger.Info("Adding \"{0}\" to PinballX database at {1}", game.Description, xmlPath);

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

		public void RemoveGame(PinballXGame game)
		{
			// read current xml
			var xmlPath = Path.Combine(game.System.DatabasePath, _settingsManager.Settings.XmlFile[game.System.Type] + ".xml");
			_logger.Info("Removing \"{0}\" from PinballX database at {1}", game.Description, xmlPath);

			if (_settingsManager.Settings.ReformatXml) {
				var menu = _marshallManager.UnmarshallXml(xmlPath);

				var gameToRemove = menu.Games.FirstOrDefault(g => g.Description == game.Description && g.FileName == game.FileName);
				if (gameToRemove == null) {
					_logger.Warn("Could not find game in existing XML, aborting.");
					return;
				}

				// remove game
				menu.Games.Remove(gameToRemove);

				// save xml
				_marshallManager.MarshallXml(menu, xmlPath);

			} else {

				// match with regex
				var xml = _file.ReadAllText(xmlPath);
				var gameBlock = new Regex("<game\\s[^>]*name=\"" + Regex.Escape(game.FileName) + "\".*?<\\/game> *[\\n\\r]?", RegexOptions.IgnoreCase | RegexOptions.Singleline);
				_logger.Info("Trying to match {0}", gameBlock);
				var match = gameBlock.Match(xml);
				var offset = 0;
				while (match.Success)  {
					if (match.Groups[0].Value.Contains("<description>" + game.Description + "</description>")) {
						_logger.Info("Got match: {0}", match.Groups[0]);
						xml = xml.Substring(0, match.Index - offset) + xml.Substring(match.Index + match.Length - offset);
						offset += match.Length;
					}
					match = match.NextMatch();
				}

				// write back to disk
				_file.WriteAllText(xmlPath, xml);
			}
		}

		public IMenuManager RenameGame(string oldFileName, AggregatedGame game)
		{
			var xmlPath = Path.Combine(game.System.DatabasePath, game.XmlGame.DatabaseFile);
			var newFilename = Path.GetFileNameWithoutExtension(game.FileName);

			if (_settingsManager.Settings.ReformatXml) {

				// read xml
				var menu = _marshallManager.UnmarshallXml(xmlPath);

				// get game
				var xmlGame = menu.Games.FirstOrDefault(g => g.FileName == oldFileName);
				if (xmlGame == null) {
					throw new InvalidOperationException($"Cannot find game with name {oldFileName} in {xmlPath}.");
				}

				// update game
				xmlGame.FileName = newFilename;

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

		public PinballXGame NewGame(Job job)
		{
			return new PinballXGame() {
				FileName = Path.GetFileNameWithoutExtension(job.FilePath),
				Description = job.Release.Game.DisplayName,
				Manufacturer = job.Release.Game.Manufacturer,
				Year = job.Release.Game.Year.ToString()
			};
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
			var parsedSystems = ParseSystems(iniPath);
			var added = 0;
			var updated = 0;

			// treat result back on main thread
			_threadManager.MainDispatcher.Invoke(() => {
				if (Systems.IsEmpty) {
					using (Systems.SuppressChangeNotifications()) {
						parsedSystems.ForEach(InitSystem);
						Systems.AddRange(parsedSystems);
					}
					return;
				}
				var remainingSystems = new HashSet<string>(Systems.Select(s => s.Name));
				foreach (var parsedSystem in parsedSystems) {
					var oldSystem = Systems.FirstOrDefault(s => s.Name == parsedSystem.Name);
					if (oldSystem == null) {
						InitSystem(parsedSystem);
						Systems.Add(parsedSystem);
						added++;
					} else if (!oldSystem.Equals(parsedSystem)) {
						oldSystem.Update(parsedSystem);
						remainingSystems.Remove(oldSystem.Name);
						updated++;
					} else {
						remainingSystems.Remove(oldSystem.Name);
					}
				}
				foreach (var removedSystem in remainingSystems) {
					Systems.Remove(Systems.FirstOrDefault(s => s.Name == removedSystem));
				}
				_logger.Info("{0} systems updated, {1} added, {2} removed.", updated, added, remainingSystems.Count);
			});
		}

		/// <summary>
		/// Enables XML database and table folder watching for the given system.
		/// </summary>
		/// <param name="system">System to initialize</param>
		private void InitSystem(PinballXSystem system) {

			// xml database watching (triggered on change of system's `Enabled`)
			system.Initialize();

			// folder watching: submit all systems to IFileSystemWatcher to figure out what to do
			system.WhenAnyValue(s => s.Enabled, s => s.TablePath).Subscribe(_ => _watcher.WatchTables(Systems));
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

			// kick off 
			if (system.Enabled) {
				// current games
				system.Games.Keys.ToList().ForEach(databaseFile => {
					_gamesUpdated.OnNext(new Tuple<PinballXSystem, string, List<PinballXGame>>(system, databaseFile, system.Games[databaseFile]));
				});

				// current mappings
				_mappingsUpdated.OnNext(new Tuple<PinballXSystem, List<Mapping>>(system, system.Mappings.ToList()));
			}

			// relay future changes
			var systemDisponsable = new CompositeDisposable {
				system.GamesUpdated.Subscribe(x => _gamesUpdated.OnNext(new Tuple<PinballXSystem, string, List<PinballXGame>>(system, x.Item1, x.Item2))),
				system.MappingsUpdated.Subscribe(mappings => _mappingsUpdated.OnNext(new Tuple<PinballXSystem, List<Mapping>>(system, mappings)))
			};
			_systemDisposables.Add(system, systemDisponsable);
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

		/// <summary>
		/// Parses PinballX.ini and reads all systems from it.
		/// </summary>
		/// <returns>Parsed systems</returns>
		private List<PinballXSystem> ParseSystems(string iniPath)
		{
			var systems = new List<PinballXSystem>();
			// only notify after this block

			var data = _marshallManager.ParseIni(iniPath);
			if (data != null) {
				if (data["VisualPinball"] != null) {
					systems.Add(new PinballXSystem(Platform.VP, data["VisualPinball"]));
				}
				if (data["FuturePinball"] != null) {
					systems.Add(new PinballXSystem(Platform.FP, data["FuturePinball"]));
				}
				
				for (var i = 0; i < 20; i++) {
					var systemName = "System_" + i;
					if (data[systemName] != null && data[systemName].Count > 0) {
						systems.Add(new PinballXSystem(data[systemName]));
					}
				}
			}
			_logger.Info("Parsed {0} systems from {1}.", systems.Count, iniPath);

			return systems;
		}
	}
}
