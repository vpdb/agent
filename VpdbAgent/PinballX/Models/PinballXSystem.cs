using IniParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.Data.Objects;
using ILogger = NLog.ILogger;

namespace VpdbAgent.PinballX.Models
{
	/// <summary>
	/// A "system" as read from PinballX.
	/// </summary>
	/// 
	/// <remarks>
	/// This comes live from PinballX.ini and resides only in memory. It's 
	/// updated when PinballX.ini changes.
	/// 
	/// As soon as it's instantiated, its XML database files are parsed and
	/// watched, meaning at every moment, current games can be retrieved through
	/// <see cref="Games"/> and future changes through <see cref="GamesUpdated"/>.
	/// </remarks>
	public class PinballXSystem : ReactiveObject, IDisposable
	{
		// deps
		private readonly ISettingsManager _settingsManager;
		private readonly IMarshallManager _marshallManager;
		private readonly ILogger _logger;
		private readonly IDirectory _dir;

		// from pinballx.ini
		public string Name { get; set; }
		public bool Enabled { get { return _enabled; } set { this.RaiseAndSetIfChanged(ref _enabled, value); } }
		public string WorkingPath { get; set; }
		public string TablePath { get { return _tablePath; } set { this.RaiseAndSetIfChanged(ref _tablePath, value); } }
		public string Executable { get { return _executable; } set { this.RaiseAndSetIfChanged(ref _executable, value); } }
		public string Parameters { get; set; }
		public PlatformType Type { get { return _type; } set { this.RaiseAndSetIfChanged(ref _type, value); } }

		/// <summary>
		/// Read-only cache of parsed games.
		/// </summary>
		/// <remarks>
		/// The main purpose of this is being able kick-off the list of games
		/// and to update <see cref="PinballXGame.DatabaseFile"/> when the XML
		/// is renamed.
		/// </remarks>
		public Dictionary<string, List<PinballXGame>> Games { get; } = new Dictionary<string, List<PinballXGame>>();

		/// <summary>
		/// Produces a value every time any data in any database file changes,
		/// or if database files are removed, added or renamed.
		/// </summary>
		/// <remarks>
		/// Note that the returned the list of games is always exhaustive, i.e. games 
		/// not in that list for the given database file are to be removed. For 
		/// example, when a database file is deleted, this will produce a Tuple with 
		/// the deleted database filename and an empty list.
		/// </remarks>
		public IObservable<Tuple<string, List<PinballXGame>>> GamesUpdated => _gamesUpdated;
		
		// convenient props
		public string DatabasePath { get; set; }
		public string MediaPath { get; set; }

		// watched props
		private bool _enabled;
		private string _tablePath;
		private string _executable;
		private PlatformType _type;

		// internal props
		private System.IO.FileSystemWatcher _fsw;
		private readonly Subject<Tuple<string, List<PinballXGame>>> _gamesUpdated = new Subject<Tuple<string, List<PinballXGame>>>();
		private readonly CompositeDisposable _disposables = new CompositeDisposable();

		/// <summary>
		/// Base constructor
		/// </summary>
		private PinballXSystem(ISettingsManager settingsManager, IMarshallManager marshallManager, ILogger logger, IDirectory dir)
		{
			_settingsManager = settingsManager;
			_marshallManager = marshallManager;
			_logger = logger;
			_dir = dir;
		}

		/// <summary>
		/// Constructs by custom system data ([System_0] - [System_9]).
		/// </summary>
		/// <param name="data">Data of .ini section</param>
		/// <param name="settingsManager">Settings dependency</param>
		/// <param name="marshallManager">Marshaller dependency</param>
		/// <param name="logger">Logger dependency</param>
		/// <param name="dir">Directory wrapper dependency</param>
		public PinballXSystem(KeyDataCollection data, ISettingsManager settingsManager, IMarshallManager marshallManager, ILogger logger, IDirectory dir) : this(settingsManager, marshallManager, logger, dir)
		{
			var systemType = data["SystemType"];
			if ("0".Equals(systemType)) {
				Type = PlatformType.Custom;
			} else if ("1".Equals(systemType)) {
				Type = PlatformType.VP;
			} else if ("2".Equals(systemType)) {
				Type = PlatformType.FP;
			}
			Name = data["Name"];

			SetByData(data);
			Initialize();
		}

		/// <summary>
		/// Constructs by default system data ([VisualPinball], [FuturePinball]).
		/// </summary>
		/// <param name="type">System type</param>
		/// <param name="data">Data of .ini section</param>
		/// <param name="settingsManager">Settings dependency</param>
		/// <param name="marshallManager">Marshaller dependency</param>
		/// <param name="logger">Logger dependency</param>
		/// <param name="dir">Directory wrapper dependency</param>
		public PinballXSystem(PlatformType type, KeyDataCollection data, ISettingsManager settingsManager, IMarshallManager marshallManager, ILogger logger, IDirectory dir) : this(settingsManager, marshallManager, logger, dir)
		{
			Type = type;
			switch (type) {
				case PlatformType.VP:
					Name = "Visual Pinball";
					break;
				case PlatformType.FP:
					Name = "Future Pinball";
					break;
				case PlatformType.Custom:
					Name = "Custom";
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, null);
			}
			SetByData(data);
			Initialize();
		}

		/// <summary>
		/// Parses all games from all available XML database files and sets up
		/// file system watchers.
		/// </summary>
		private void Initialize()
		{
			if (_dir.Exists(DatabasePath)) {

				// kick off
				GetDatbaseFiles().ForEach(UpdateGames);

				// then setup watchers
				_fsw = new System.IO.FileSystemWatcher(DatabasePath, "*.xml");
				_logger.Info("Watching XML files at {0}...", DatabasePath);
				_disposables.Add(_fsw);

				// file changed
				_disposables.Add(Observable
						.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => _fsw.Changed += x, x => _fsw.Changed -= x)
						.Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
						.Where(x => x.EventArgs.FullPath != null)
						.Subscribe(x => UpdateGames(x.EventArgs.FullPath)));

				// file created
				_disposables.Add(Observable
						.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => _fsw.Created += x, x => _fsw.Created -= x)
						.Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
						.Subscribe(x => UpdateGames(x.EventArgs.FullPath)));

				// file deleted
				_disposables.Add(Observable
						.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => _fsw.Deleted += x, x => _fsw.Deleted -= x)
						.Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
						.Subscribe(x => RemoveGames(x.EventArgs.FullPath)));

				// file renamed
				_disposables.Add(Observable
						.FromEventPattern<RenamedEventHandler, FileSystemEventArgs>(x => _fsw.Renamed += x, x => _fsw.Renamed -= x)
						.Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
						.Subscribe(x => RenameDatabase(((RenamedEventArgs)x.EventArgs).OldFullPath, x.EventArgs.FullPath)));

				_fsw.EnableRaisingEvents = true;

			} else {
				_logger.Warn("Invalid database path \"{0}\" for system \"{1}\", ignoring.", DatabasePath, Name);
			}
		}

		/// <summary>
		/// Updates all games of a given XML database file.
		/// </summary>
		/// <remarks>
		/// Updating means parsing data from the XML file, saving it in <see cref="Games"/>
		/// and triggering an update through <see cref="GamesUpdated"/>.
		/// </remarks>
		/// <param name="databaseFilePath">Full path to database file</param>
		private void UpdateGames(string databaseFilePath)
		{
			if (databaseFilePath == null) {
				_logger.Warn("Ignoring null value for path from file watcher.");
				return;
			}
			var databaseFile = Path.GetFileName(databaseFilePath);
			if (!Enabled) {
				_logger.Info("Ignoring disabled system \"{0}\".", Name);
				_gamesUpdated.OnNext(new Tuple<string, List<PinballXGame>>(databaseFile, new List<PinballXGame>()));
				return;
			}

			// read enabled games from XML
			_logger.Info("Parsing games for {0} ({1})...", this, databaseFile);
			Games[databaseFile] = ParseGames(databaseFile);

			// filter disabled games
			var games = Games[databaseFile].Where(g => g.Enabled == null || "true".Equals(g.Enabled, StringComparison.InvariantCultureIgnoreCase)).ToList();

			// trigger update
			_gamesUpdated.OnNext(new Tuple<string, List<PinballXGame>>(databaseFile, games));
		}

		/// <summary>
		/// Removes all games of a given XML database file.
		/// </summary>
		/// <remarks>
		/// This means that the database file was deleted.
		/// </remarks>
		/// <param name="databaseFilePath">Full path to database file</param>
		private void RemoveGames(string databaseFilePath)
		{
			if (databaseFilePath == null) {
				_logger.Warn("Ignoring null value for path from file watcher.");
				return;
			}
			var databaseFile = Path.GetFileName(databaseFilePath);
			_gamesUpdated.OnNext(new Tuple<string, List<PinballXGame>>(databaseFile, new List<PinballXGame>()));
		}
		
		/// <summary>
		/// Changes the XML database file name of all games in a given XML database.
		/// </summary>
		/// <param name="oldDatabaseFilePath">Full path to old database file</param>
		/// <param name="newDatabaseFilePath">Full path to new database file</param>
		private void RenameDatabase(string oldDatabaseFilePath, string newDatabaseFilePath)
		{
			if (oldDatabaseFilePath == null || newDatabaseFilePath == null) {
				_logger.Warn("Ignoring null value for path from file watcher.");
				return;
			}
			var databaseOld = Path.GetFileName(oldDatabaseFilePath);
			var databaseNew = Path.GetFileName(newDatabaseFilePath);
			_logger.Info("PinballX database {0} renamed from {1} to {2}.", Name, databaseOld, databaseNew);
			Games[databaseOld].ToList().ForEach(g => g.DatabaseFile = databaseNew);
		}

		/// <summary>
		/// Parses all games for a given system.
		/// </summary>
		/// <remarks>
		/// "Parsing" means reading and unmarshalling all XML files in the 
		/// system's database folder.
		/// </remarks>
		/// <param name="databaseFile">If set, only parse games for given XML file</param>
		/// <returns>Parsed games</returns>
		private List<PinballXGame> ParseGames(string databaseFile = null)
		{
			_logger.Info("Parsing games at {0}", DatabasePath);

			var games = new List<PinballXGame>();
			var fileCount = 0;
			if (_dir.Exists(DatabasePath)) {
				foreach (var filePath in _dir.GetFiles(DatabasePath).Where(filePath => ".xml".Equals(Path.GetExtension(filePath), StringComparison.InvariantCultureIgnoreCase)))
				{
					var currentDatabaseFile = Path.GetFileName(filePath);
					// if database file is specified, drop everything else
					if (databaseFile != null && !databaseFile.Equals(currentDatabaseFile)) {
						continue;
					}
					var menu = _marshallManager.UnmarshallXml(filePath);
					menu.Games.ForEach(game => {
						game.PinballXSystem = this;
						game.DatabaseFile = currentDatabaseFile;
					});
					games.AddRange(menu.Games);
					fileCount++;
				}
			}
			_logger.Debug("Parsed {0} games from {1} XML file(s) at {2}.", games.Count, fileCount, DatabasePath);
			return games;
		}
		
		/// <summary>
		/// Copies data over from another system
		/// </summary>
		/// <param name="system">New system</param>
		/// <returns></returns>
		public PinballXSystem Update(PinballXSystem system)
		{
			Enabled = system.Enabled;
			WorkingPath = system.WorkingPath;
			TablePath = system.TablePath;
			Executable = system.Executable;
			Parameters = system.Parameters;
			Type = system.Type;
			return this;
		}

		/// <summary>
		/// Copies system data from PinballX.ini to our object.
		/// TODO validate (e.g. no TablePath should result at least in an error (now it crashes))
		/// </summary>
		/// <param name="data">Parsed data</param>
		private void SetByData(KeyDataCollection data)
		{
			Enabled = "true".Equals(data["Enabled"], StringComparison.InvariantCultureIgnoreCase);
			WorkingPath = data["WorkingPath"];
			TablePath = data["TablePath"];
			Executable = data["Executable"];
			Parameters = data["Parameters"];

			DatabasePath = Path.Combine(_settingsManager.Settings.PbxFolder, "Databases", Name);
			MediaPath = Path.Combine(_settingsManager.Settings.PbxFolder, "Media", Name);
		}

		/// <summary>
		/// Returns all database files of this system.
		/// </summary>
		/// <returns>List of full paths to database files</returns>
		private List<string> GetDatbaseFiles()
		{
			return _dir
				.GetFiles(DatabasePath)
				.Where(filePath => ".xml".Equals(Path.GetExtension(filePath), StringComparison.InvariantCultureIgnoreCase)) 
				.ToList();
		} 

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) {
				return false;
			}
			if (ReferenceEquals(this, obj)) {
				return true;
			}
			var system = obj as PinballXSystem;
			if (system == null) {
				return false;
			}
			return 
				Name == system.Name &&
				Enabled == system.Enabled &&
				WorkingPath == system.WorkingPath &&
				TablePath == system.TablePath &&
				Executable == system.Executable &&
				Parameters == system.Parameters &&
				Type == system.Type;
		}

		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return Name.GetHashCode();
		}

		public void Dispose()
		{
			_disposables.Dispose();
		}

		public override string ToString()
		{
			return $"System {Name}";
		}
	}
}
