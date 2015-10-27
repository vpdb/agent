using IniParser;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using ReactiveUI;
using VpdbAgent.PinballX.Models;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Windows;
using VpdbAgent.Application;

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
		/// <returns></returns>
		IMenuManager Initialize();

		/// <summary>
		/// Systems parsed from <c>PinballX.ini</c>.
		/// </summary>
		ReactiveList<PinballXSystem> Systems { get; }
	}

	/// <summary>
	/// Application logic for <see cref="IMenuManager"/>.
	/// </summary>
	public class MenuManager : IMenuManager
	{
		public ReactiveList<PinballXSystem> Systems { get; } = new ReactiveList<PinballXSystem>();

		// dependencies
		private readonly IFileSystemWatcher _watcher;
		private readonly ISettingsManager _settingsManager;
		private readonly Logger _logger;

		public MenuManager(IFileSystemWatcher fileSystemWatcher, ISettingsManager settingsManager, Logger logger)
		{
			_watcher = fileSystemWatcher;
			_settingsManager = settingsManager;
			_logger = logger;
		}
	
		public IMenuManager Initialize()
		{
			// settings must be initialized before doing this.
			if (!_settingsManager.IsInitialized()) {
				return this;
			}

			var iniPath = _settingsManager.PbxFolder + @"\Config\PinballX.ini";
			var dbPath = _settingsManager.PbxFolder + @"\Databases\";

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
			Systems.Changed.Subscribe(systems => {
				xmlWatcher?.Dispose();
				xmlWatcher = _watcher.DatabaseWatcher(dbPath, Systems)
					.ObserveOn(Scheduler.Default)
					.Select(path => new { path, system = Systems.FirstOrDefault(s => s.DatabasePath.Equals(Path.GetDirectoryName(path))) })
					.Subscribe(x => UpdateGames(x.system, Path.GetFileName(x.path)));
			});

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
		private IEnumerable<Game> ParseGames(PinballXSystem system, string databaseFile = null)
		{
			if (system == null) {
				_logger.Warn("Unknown system, not parsing games.");
				return new List<Game>();
			}
			_logger.Info("Parsing games at {0}", system.DatabasePath);

			var games = new List<Game>();
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
		/// Returns an unmarshaled object for a given .XML file
		/// </summary>
		/// <param name="filepath">Absolute path to the .XML file</param>
		/// <returns></returns>
		private Menu UnmarshallXml(string filepath)
		{
			var menu = new Menu();
			Stream reader = null;
			try {
				var serializer = new XmlSerializer(typeof(Menu));
				reader = new FileStream(filepath, FileMode.Open);
				menu = serializer.Deserialize(reader) as Menu;

			} catch (Exception e) {
				_logger.Error(e, "Error parsing {0}: {1}", filepath, e.Message);

			} finally {
				reader?.Close();
			}
			return menu;
		}
	}
}
