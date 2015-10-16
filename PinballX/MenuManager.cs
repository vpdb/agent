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
using System.Reactive.Subjects;
using System.Windows;

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
	/// read-only maintenance of the user's configuration. See GameManager for
	/// data structures that are actually used in the application.
	/// </remarks>
	public class MenuManager : IMenuManager
	{
	
		/// <summary>
		/// Systems parsed from PinballX.ini.
		/// </summary>
		public ReactiveList<PinballXSystem> Systems { get; } = new ReactiveList<PinballXSystem>();

		// game change handlers
		private readonly Subject<PinballXSystem> _gamesChanged = new Subject<PinballXSystem>();
		public IObservable<PinballXSystem> GamesChanged => _gamesChanged.AsObservable();

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

		/// <summary>
		/// Starts watching file system for configuration changes and triggers an
		/// initial update.
		/// </summary>
		/// <returns></returns>
		public IMenuManager Initialize()
		{
			if (!_settingsManager.IsInitialized())
			{
				return this;
			}

			var iniPath = _settingsManager.PbxFolder + @"\Config\PinballX.ini";
			var dbPath = _settingsManager.PbxFolder + @"\Databases\";

			// update systems when ini changes (or now)
			_watcher.FileWatcher(iniPath)
				.StartWith(iniPath)                // kick-off without waiting for first file change
				.SubscribeOn(Scheduler.Default)    // do work on background thread
				.Subscribe(UpdateSystems);

			// parse games when systems change
			Systems.Changed
				.ObserveOn(Scheduler.Default)
				.Subscribe(UpdateGames);

			Systems.ItemsAdded
				.ObserveOn(Scheduler.Default)
				.Subscribe(observer => {
					_logger.Info("Systems added: {0}", observer);
				});


			/*
			ParseSystemsObs(iniPath)
				.SubscribeOn(Scheduler.Default)
				.Subscribe(systems =>
				{
					// setup watchers
					// todo DISPOSE!
					_watcher.DatabaseWatcher(dbPath, systems)
						.Select(Path.GetDirectoryName)
						.Select(path => systems.FirstOrDefault(s => s.DatabasePath.Equals(path)))
						.Subscribe(UpdateGames);

					// parse all games when systems change
					systems.Changed
						.Subscribe(_ => UpdateGames());

					// kick off initially parse
					foreach (var system in Systems) {
						UpdateGames(system);
					}
				}); */


			return this;
		}

		private void UpdateSystems(string iniPath)
		{
			// here, we're on a worker thread.
			var systems = ParseSystems(iniPath);

			// treat result back on main thread
			Application.Current.Dispatcher.Invoke((Action)delegate
			{
				using (Systems.SuppressChangeNotifications()) {
					// todo make this more intelligent by diff'ing and changing instead of drop-and-create
					Systems.Clear();
					Systems.AddRange(systems);
				}
			});
		}

		private void UpdateGames(NotifyCollectionChangedEventArgs args)
		{
			_logger.Info("Parsing all games for all systems...");
			foreach (var system in Systems) {

				var games = ParseGames(system);
				using (system.Games.SuppressChangeNotifications()) {
					// todo make this more intelligent by diff'ing and changing instead of drop-and-create
					system.Games.Clear();
					system.Games.AddRange(games);
				}
			}
			_logger.Info("All games parsed.");
		}

		/// <summary>
		/// Parses PinballX.ini and reads all systems from it.
		/// </summary>
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
		/// Parses all games at a given path.
		/// </summary>
		/// <param name="system">System to parse games for</param>
		private IEnumerable<Game> ParseGames(PinballXSystem system)
		{
			if (system == null) {
				_logger.Warn("Unknown system, not parsing games.");
				return new List<Game>();
			}
			_logger.Info("Parsing games at {0}", system.DatabasePath);

			var games = new List<Game>();
			var fileCount = 0;
			if (Directory.Exists(system.DatabasePath)) {
				foreach (var filePath in Directory.GetFiles(system.DatabasePath)
					.Where(filePath => ".xml".Equals(Path.GetExtension(filePath), StringComparison.InvariantCultureIgnoreCase))) {
					games.AddRange(UnmarshalXml(filePath).Games);
					fileCount++;
				}
			}
			_logger.Debug("Parsed {0} games from {1} XML files at {2}.", games.Count, fileCount, system.DatabasePath);

			return games;
			// announce to subscribers
			//_gamesChanged.OnNext(system);
		}

		/// <summary>
		/// Returns an unmarshaled object for a given .XML file
		/// </summary>
		/// <param name="filepath">Absolute path to the .XML file</param>
		/// <returns></returns>
		private Menu UnmarshalXml(string filepath)
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

	public interface IMenuManager
	{
		ReactiveList<PinballXSystem> Systems { get; }
		IObservable<PinballXSystem> GamesChanged { get; }
		IMenuManager Initialize();
	}
}
