using IniParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using JetBrains.Annotations;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.Data;
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
	/// When it's initialized, its XML database files are parsed and watched, 
	/// meaning as soon as it's part of <see cref="PinballXManager.Systems"/>, 
	/// current games can be retrieved through <see cref="Games"/> and future
	/// changes through <see cref="GamesUpdated"/>.
	/// </remarks>
	public class PinballXSystem : ReactiveObject, IDisposable
	{

		public const string DefaultExecutableLabel = "<default>";

		// from pinballx.ini
		public string Name { get; set; }
		public bool Enabled { get { return _enabled; } set { this.RaiseAndSetIfChanged(ref _enabled, value); } }
		public string WorkingPath { get; set; }
		public string TablePath { get { return _tablePath; } set { this.RaiseAndSetIfChanged(ref _tablePath, value); } }
		public string Executable { get { return _executable; } set { this.RaiseAndSetIfChanged(ref _executable, value); } }
		public string Parameters { get; set; }
		public Platform Type { get { return _type; } set { this.RaiseAndSetIfChanged(ref _type, value); } }

		/// <summary>
		/// Read-only cache of parsed games.
		/// </summary>
		/// 
		/// <remarks>
		/// The main purpose of this is being able kick-off the list of games
		/// and to update <see cref="PinballXGame.DatabaseFile"/> when the XML
		/// is renamed.
		/// </remarks>
		public Dictionary<string, List<PinballXGame>> Games { get; } = new Dictionary<string, List<PinballXGame>>();

		/// <summary>
		/// A list of database files (without path) of this system.
		/// </summary>
		public IReactiveList<string> DatabaseFiles { get; } = new ReactiveList<string>();

		/// <summary>
		/// A list of executables used in this system. Note that null string equals default executable.
		/// </summary>
		public IReactiveList<string> Executables { get; } = new ReactiveList<string>();

		/// <summary>
		/// Mappings of this system. Adding, removing and updating stuff from here
		/// will result in the mappings being written to disk.
		/// </summary>
		public IReactiveList<Mapping> Mappings => _mapping.Mappings;

		/// <summary>
		/// Produces a value every time any data in any XML database file changes,
		/// or if database files are removed, added or renamed.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that the returned the list of games is always exhaustive, i.e. games 
		/// not in that list for the given database file are to be removed. For 
		/// example, when a database file is deleted, this will produce a Tuple with 
		/// the deleted database filename and an empty list.
		/// </remarks>
		public IObservable<Tuple<string, List<PinballXGame>>> GamesUpdated => _gamesUpdated;

		/// <summary>
		/// Produces a value every time any mapping data is added, removed, or modified.
		/// </summary>
		/// 
		/// <remarks>
		/// Basically if `vpdb.json` is added, modified or deleted, this is 
		/// triggered.
		/// 
		/// Note that the returned list of mappings is exhaustive, i.e. mappings not
		/// in that list are to be removed.
		/// </remarks>
		public IObservable<List<Mapping>> MappingsUpdated => _mappingsUpdated;

		// convenient props
		public string DatabasePath { get; private set; }
		public string MediaPath { get; private set; }
		public string MappingPath => Path.Combine(DatabasePath, "vpdb.json");

		// watched props
		private bool _enabled;
		private string _tablePath;
		private string _executable;
		private Platform _type;

		// internal props
		private System.IO.FileSystemWatcher _fsw;
		private readonly Subject<Tuple<string, List<PinballXGame>>> _gamesUpdated = new Subject<Tuple<string, List<PinballXGame>>>();
		private readonly Subject<List<Mapping>> _mappingsUpdated = new Subject<List<Mapping>>();
		private readonly CompositeDisposable _databaseWatchers = new CompositeDisposable();
		private readonly SystemMapping _mapping;
		private IDisposable _mappingFileWatcher;

		// deps
		private readonly ISettingsManager _settingsManager;
		private readonly IMarshallManager _marshallManager;
		private readonly IFileSystemWatcher _watcher;
		private readonly IThreadManager _threadManager;
		private readonly ILogger _logger;
		private readonly IDirectory _dir;
		private readonly IFile _file;

		/// <summary>
		/// Base constructor
		/// </summary>
		private PinballXSystem(IDependencyResolver resolver)
		{
			_settingsManager = resolver.GetService<ISettingsManager>();
			_marshallManager = resolver.GetService<IMarshallManager>();
			_threadManager = resolver.GetService<IThreadManager>();
			_watcher = resolver.GetService<IFileSystemWatcher>();
			_logger = resolver.GetService<ILogger>();
			_file = resolver.GetService<IFile>();
			_dir = resolver.GetService<IDirectory>();
		}

		/// <summary>
		/// Constructs by custom system data ([System_0] - [System_9]).
		/// </summary>
		/// <param name="data">Data of .ini section</param>
		public PinballXSystem(KeyDataCollection data) : this(Locator.Current)
		{
			var systemType = data["SystemType"];
			if ("0".Equals(systemType)) {
				Type = Platform.Custom;
			} else if ("1".Equals(systemType)) {
				Type = Platform.VP;
			} else if ("2".Equals(systemType)) {
				Type = Platform.FP;
			}
			Name = data["Name"];

			SetByData(data);
			_mapping = new SystemMapping(MappingPath, _marshallManager);
		}

		/// <summary>
		/// Constructs by default system data ([VisualPinball], [FuturePinball]).
		/// </summary>
		/// <param name="type">System type</param>
		/// <param name="data">Data of .ini section</param>
		public PinballXSystem(Platform type, KeyDataCollection data) : this(Locator.Current)
		{
			Type = type;
			switch (type) {
				case Platform.VP:
					Name = "Visual Pinball";
					break;
				case Platform.FP:
					Name = "Future Pinball";
					break;
				case Platform.Custom:
					Name = "Custom";
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, null);
			}
			SetByData(data);
			_mapping = new SystemMapping(MappingPath, _marshallManager);
		}

		/// <summary>
		/// Sets up a watcher for <see cref="Enabled"/> that either creates or 
		/// destroys the XML database watchers.
		/// </summary>
		public void Initialize()
		{
			if (!_dir.Exists(DatabasePath)) {
				_logger.Warn("Invalid database path \"{0}\" for {1}, ignoring.", DatabasePath, this);
				return;
			}

			// watch xml database files and mappings
			this.WhenAnyValue(s => s.Enabled).Subscribe(enabled => {

				// kick off and watch
				if (enabled) {
					
					// xml database
					GetDatabaseFiles().ForEach(UpdateGames);
					EnableDatabaseWatchers();

					// mappings
					UpdateOrRemoveMappings(MappingPath);
					_mappingFileWatcher = _watcher.FileWatcher(MappingPath).Sample(TimeSpan.FromMilliseconds(100)).Subscribe(UpdateOrRemoveMappings);

				} else {
					// clear games and destroy watchers
					GetDatabaseFiles().ForEach(RemoveGames);
					DisableDatabaseWatchers();
					_mappingFileWatcher?.Dispose();
				}
			});
		}

		/// <summary>
		/// Sets up file system watchers for XML database files.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that we don't manage watching of pinball tables here, because
		/// multiple system can use the same table folder, which would result 
		/// in duplicate watchers. Thus, table files are watched at 
		/// <see cref="FileSystemWatcher"/>.
		/// </remarks>
		private void EnableDatabaseWatchers()
		{
			// setup watchers
			_fsw = new System.IO.FileSystemWatcher(DatabasePath, "*.xml");
			_logger.Info("Watching XML files at {0}...", DatabasePath);
			_databaseWatchers.Add(_fsw);

			var trottle = TimeSpan.FromMilliseconds(100); // file changes are triggered multiple times
			var delay = TimeSpan.FromMilliseconds(500);  // avoid concurrent read/write access

			// file changed
			_databaseWatchers.Add(Observable
					.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => _fsw.Changed += x, x => _fsw.Changed -= x)
					.Throttle(trottle, RxApp.TaskpoolScheduler)
					.Where(x => x.EventArgs.FullPath != null)
					.Delay(delay)
					.Subscribe(x => UpdateGames(x.EventArgs.FullPath)));

			// file created
			_databaseWatchers.Add(Observable
					.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => _fsw.Created += x, x => _fsw.Created -= x)
					.Throttle(trottle, RxApp.TaskpoolScheduler)
					.Delay(delay)
					.Subscribe(x => UpdateGames(x.EventArgs.FullPath)));

			// file deleted
			_databaseWatchers.Add(Observable
					.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => _fsw.Deleted += x, x => _fsw.Deleted -= x)
					.Throttle(trottle, RxApp.TaskpoolScheduler)
					.Delay(delay)
					.Subscribe(x => RemoveGames(x.EventArgs.FullPath)));

			// file renamed
			_databaseWatchers.Add(Observable
					.FromEventPattern<RenamedEventHandler, FileSystemEventArgs>(x => _fsw.Renamed += x, x => _fsw.Renamed -= x)
					.Throttle(trottle, RxApp.TaskpoolScheduler)
					.Delay(delay)
					.Subscribe(x => RenameDatabase(((RenamedEventArgs)x.EventArgs).OldFullPath, x.EventArgs.FullPath)));

			_fsw.EnableRaisingEvents = true;
		}

		/// <summary>
		/// Destroys all file watchers.
		/// </summary>
		private void DisableDatabaseWatchers()
		{
			if (_databaseWatchers.Count > 0) {
				_logger.Info("Stopped watching XML files at {0}...", DatabasePath);
			}
			_databaseWatchers.Clear();
		}

		/// <summary>
		/// Updates all games of a given XML database file.
		/// </summary>
		/// 
		/// <remarks>
		/// Updating means parsing data from the XML file, saving it in <see cref="Games"/>
		/// and triggering an update through <see cref="GamesUpdated"/>.
		/// </remarks>
		/// 
		/// <param name="databaseFilePath">Full path to database file</param>
		private void UpdateGames(string databaseFilePath)
		{
			if (databaseFilePath == null) {
				_logger.Warn("Ignoring null value for path from file watcher.");
				return;
			}
			var databaseFile = Path.GetFileName(databaseFilePath);

			// read enabled games from XML
			Games[databaseFile] = ParseGames(databaseFile);

			// update list of database files
			if (!DatabaseFiles.Contains(databaseFile)) {
				_threadManager.MainDispatcher.Invoke(() => DatabaseFiles.Add(databaseFile));
			}

			// trigger update
			_gamesUpdated.OnNext(new Tuple<string, List<PinballXGame>>(databaseFile, Games[databaseFile]));
		}

		/// <summary>
		/// Removes all games of a given XML database file.
		/// </summary>
		/// 
		/// <remarks>
		/// This means that the database file was deleted.
		/// </remarks>
		/// 
		/// <param name="databaseFilePath">Full path to database file</param>
		private void RemoveGames(string databaseFilePath)
		{
			if (databaseFilePath == null) {
				_logger.Warn("Ignoring null value for path from file watcher.");
				return;
			}
			var databaseFile = Path.GetFileName(databaseFilePath);

			// update database files
			_threadManager.MainDispatcher.Invoke(() => DatabaseFiles.Remove(databaseFile));

			_gamesUpdated.OnNext(new Tuple<string, List<PinballXGame>>(databaseFile, new List<PinballXGame>()));
		}

		/// <summary>
		/// Gets executed every time the mapping file is created, changes, or 
		/// is removed.
		/// </summary>
		private void UpdateOrRemoveMappings(string path)
		{
			if (_file.Exists(path)) {

				var mapping = _marshallManager.UnmarshallMappings(path, this);
				foreach (var m in mapping.Mappings) {
					m.System = this;
				}

				// update self-saving mapping (should not trigger save because we don't subscribe to ShouldReset, which is triggered when using SuppressChangeNotifications).
				using (_mapping.Mappings.SuppressChangeNotifications()) {
					_mapping.Mappings.Clear();
					foreach (var m in mapping.Mappings) {
						_mapping.Mappings.Add(m);
					}
				}
				_mappingsUpdated.OnNext(_mapping.Mappings.ToList());

			} else {
				_mappingsUpdated.OnNext(new List<Mapping>());
			}
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
			
			// update properties of concerned games
			Games[databaseOld].ToList().ForEach(g => g.DatabaseFile = databaseNew);

			// update database file list
			_threadManager.MainDispatcher.Invoke(() => {
				DatabaseFiles.Remove(databaseOld);
				DatabaseFiles.Add(databaseNew);
			});

			// rename key
			Games[databaseNew] = Games[databaseOld];
			Games.Remove(databaseOld);
		}

		/// <summary>
		/// Parses all games for a given system.
		/// </summary>
		/// 
		/// <remarks>
		/// "Parsing" means reading and unmarshalling the given XML file in the 
		/// system's database folder.
		/// </remarks>
		/// 
		/// <param name="databaseFile">XML file to parse</param>
		/// <returns>Parsed games</returns>
		private List<PinballXGame> ParseGames([NotNull] string databaseFile)
		{
			var xmlPath = Path.Combine(DatabasePath, databaseFile);
			var games = new List<PinballXGame>();
			if (_dir.Exists(DatabasePath)) {
				var menu = _marshallManager.UnmarshallXml(xmlPath);
				var remainingExecutables = Executables.ToList();
				menu.Games.ForEach(game => {

					// update links
					game.System = this;
					game.DatabaseFile = databaseFile;

					// update executables
					var executable = game.AlternateExe != null && game.AlternateExe.Trim() != "" ? game.AlternateExe : DefaultExecutableLabel;
					if (!Executables.Contains(executable)) {
						_logger.Debug("Adding new alternate executable \"{0}\" to system \"{1}\"", executable, Name);
						_threadManager.MainDispatcher.Invoke(() => Executables.Add(executable));
					} else {
						if (remainingExecutables.Contains(executable)) {
							remainingExecutables.Remove(executable);
						}
					}
				});
				_logger.Debug("Removing alternate executables [ \"{0}\" ] from system \"{1}\"", string.Join("\", \"", remainingExecutables), Name);
				_threadManager.MainDispatcher.Invoke(() => Executables.RemoveAll(remainingExecutables));
				_threadManager.MainDispatcher.Invoke(() => Executables.Sort(string.Compare));

				games.AddRange(menu.Games);
			}
			_logger.Debug("Parsed {0} games from {1}.", games.Count, xmlPath);
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

			DatabasePath = PathHelper.NormalizePath(Path.Combine(_settingsManager.Settings.PinballXFolder, "Databases", Name));
			MediaPath = PathHelper.NormalizePath(Path.Combine(_settingsManager.Settings.PinballXFolder, "Media", Name));
		}

		/// <summary>
		/// Returns all database files of this system.
		/// </summary>
		/// <returns>List of full paths to database files</returns>
		private List<string> GetDatabaseFiles()
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
			_databaseWatchers.Dispose();
			_mappingFileWatcher.Dispose();
		}

		public override string ToString()
		{
			return $"system \"{Name}\"";
		}
	}

	/// <summary>
	/// Different types of systems.
	/// </summary>
	public enum Platform
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
