using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Newtonsoft.Json;
using NLog;
using ReactiveUI;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Download;
using VpdbAgent.Vpdb.Models;
using VpdbAgent.Vpdb.Network;
using File = System.IO.File;

namespace VpdbAgent.Application
{
	/// <summary>
	/// Manages the global stuff we're saving to disk.
	/// 
	/// Currently, it's serialized as a .json file in PinballX's database root
	/// folder. Might be switching it out for something more efficient such as
	/// protocol-buffers.
	/// </summary>
	/// <see cref="GlobalDatabase"/>
	public interface IDatabaseManager
	{
		IObservable<bool> Initialized { get; }

		/// <summary>
		/// Read data, run when settings are initialized.
		/// </summary>
		/// <returns></returns>
		IDatabaseManager Initialize();

		/// <summary>
		/// Saves data back to disk
		/// </summary>
		/// <returns></returns>
		IDatabaseManager Save();

		/// <summary>
		/// Returns the entire release object for a given release ID.
		/// </summary>
		/// <param name="releaseId">Release ID</param>
		/// <returns>Release or null if release not in database</returns>
		VpdbRelease GetRelease(string releaseId);

		/// <summary>
		/// Returns the version object for a given file of a given release.
		/// </summary>
		/// <param name="fileId">File ID</param>
		/// <param name="releaseId">Release ID</param>
		/// <returns>Version or null if either release or file is not found</returns>
		VpdbVersion GetVersion(string releaseId, string fileId);

		/// <summary>
		/// Returns the table file object for a given file ID of a given release.
		/// </summary>
		/// <param name="fileId">File ID</param>
		/// <param name="releaseId">Release ID</param>
		/// <returns></returns>
		VpdbTableFile GetTableFile(string releaseId, string fileId);

		/// <summary>
		/// Returns the file object for a given file ID that is not part of a release.
		/// </summary>
		/// <param name="fileId">File ID</param>
		/// <returns></returns>
		VpdbFile GetFile(string fileId);

		/// <summary>
		/// Updates the database with updated release data for a given release
		/// or adds it if not available.
		/// </summary>
		/// <remarks>
		/// Note that the database is NOT saved, so use <see cref="Save"/> if needed.
		/// </remarks>
		/// <param name="release">Release to update or add</param>
		/// <returns>Local game if provided or found, null otherwise</returns>
		void AddOrUpdateRelease(VpdbRelease release);

		/// <summary>
		/// Adds a new file object to the database or replaces if it exists
		/// already.
		/// </summary>
		/// <remarks>
		/// The file cache is mainly we have access to all file data of files
		/// that are not part of a release. However, for convenience reasons,
		/// we also add release file part of a download job.
		/// </remarks>
		/// <param name="file">File object</param>
		void AddOrReplaceFile(VpdbFile file);

		/// <summary>
		/// Adds a new download job to the database.
		/// </summary>
		/// <param name="job">Job to add</param>
		void AddJob(Job job);

		/// <summary>
		/// Returns all jobs in the database.
		/// </summary>
		/// <returns>Download jobs</returns>
		ReactiveList<Job> GetJobs();

		/// <summary>
		/// Removes a given download job from the database.
		/// </summary>
		/// <param name="job">Job to remove</param>
		void RemoveJob(Job job);

		/// <summary>
		/// Returns all log messages in the database.
		/// </summary>
		/// <returns>All log messages</returns>
		IReactiveList<Message> GetMessages();

		/// <summary>
		/// Adds a new message and saves database.
		/// </summary>
		/// <param name="msg">Message to log</param>
		void Log(Message msg);

		/// <summary>
		/// Removes all messages from the database.
		/// </summary>
		void ClearLog();
	}

	[ExcludeFromCodeCoverage]
	public class DatabaseManager : IDatabaseManager
	{
		// deps
		private readonly ISettingsManager _settingsManager;
		private readonly CrashManager _crashManager;
		private readonly ILogger _logger;

		// props
		public IObservable<bool> Initialized => _initialized;
		private GlobalDatabase Database { get; } = new GlobalDatabase();

		private string _dbPath;
		private readonly BehaviorSubject<bool> _initialized = new BehaviorSubject<bool>(false);
		private readonly Subject<Unit> _save = new Subject<Unit>();

		public DatabaseManager(ISettingsManager settingsManager, CrashManager crashManager, ILogger logger)
		{
			_settingsManager = settingsManager;
			_crashManager = crashManager;
			_logger = logger;

			// throttle saving
			_save
				.ObserveOn(Scheduler.Default)
				.Sample(TimeSpan.FromMilliseconds(500))
				.Subscribe(_ => { MarshallDatabase(); });
		}

		public IDatabaseManager Initialize()
		{
			_dbPath = Path.Combine(_settingsManager.Settings.PbxFolder, @"Databases\vpdb.json");
			Database.Update(UnmarshallDatabase());
			_logger.Info("Global database with {0} release(s) loaded.", Database.Releases.Count);
			_initialized.OnNext(true);
			return this;
		}

		public IDatabaseManager Save()
		{
			_save.OnNext(Unit.Default);
			return this;
		}

		public VpdbRelease GetRelease(string releaseId)
		{
			return releaseId == null || !Database.Releases.ContainsKey(releaseId) ? null : Database.Releases[releaseId];
		}

		public VpdbVersion GetVersion(string releaseId, string fileId)
		{
			if (releaseId == null || !Database.Releases.ContainsKey(releaseId)) {
				return null;
			}
			// todo add map to make it fast
			return Database.Releases[releaseId].Versions
				.FirstOrDefault(v => v.Files.Contains(v.Files.FirstOrDefault(f => f.Reference.Id == fileId)));
		}

		public VpdbTableFile GetTableFile(string releaseId, string fileId)
		{
			
			if (releaseId == null || !Database.Releases.ContainsKey(releaseId)) {
				return null;
			}
			// todo add map to make it fast
			return Database.Releases[releaseId].Versions
					.SelectMany(v => v.Files)
					.FirstOrDefault(f => f.Reference.Id == fileId);
		}

		public VpdbFile GetFile(string fileId)
		{
			if (fileId == null || !Database.Files.ContainsKey(fileId)) {
				return null;
			}
			return Database.Files[fileId];
		}

		public void AddOrUpdateRelease(VpdbRelease release)
		{
			if (!Database.Releases.ContainsKey(release.Id)) {
				_logger.Info("Adding new release data for release {0} ({1})", release.Id, release.Name);
				Database.Releases.Add(release.Id, release);

			} else {
				_logger.Info("Updating release data of release {0} ({1})", release.Id, release.Name);
				Database.Releases[release.Id].Update(release);
			}
		}

		public void AddOrReplaceFile(VpdbFile file)
		{
			if (!Database.Files.ContainsKey(file.Id)) {
				Database.Files.Add(file.Id, file);

			} else {
				Database.Files[file.Id] = file;
			}
		}

		public void AddJob(Job job)
		{
			Database.DownloadJobs.Add(job);
		}

		public ReactiveList<Job> GetJobs()
		{
			return Database.DownloadJobs;
		}

		public void RemoveJob(Job job)
		{
			Database.DownloadJobs.Remove(job);
		}

		public IReactiveList<Message> GetMessages()
		{
			return Database.Messages;
		}

		public void Log(Message msg)
		{
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				Database.Messages.Add(msg);
				Save();
			});
		}

		public void ClearLog()
		{
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				Database.Messages.Clear();
				Save();
			});
		}

		/// <summary>
		/// Reads the internal global .json file of a given platform and 
		/// returns the unmarshalled database object.
		/// </summary>
		/// <returns>Deserialized object or empty database if no file exists or parsing error</returns>
		private GlobalDatabase UnmarshallDatabase()
		{
			if (!File.Exists(_dbPath)) {
				_logger.Info("Creating new global database, {0} not found.", _dbPath);
				return null;
			}
			try {
				using (var sr = new StreamReader(_dbPath))
				using (JsonReader reader = new JsonTextReader(sr)) {
					try {
						var db = _serializer.Deserialize<GlobalDatabase>(reader);
						reader.Close();
						return db;
					} catch (Exception e) {
						_logger.Error(e, "Error parsing {0}, deleting and ignoring.", _dbPath);
						_crashManager.Report(e, "json");
						reader.Close();
						File.Delete(_dbPath);
						return null;
					}
				}
			} catch (Exception e) {
				_logger.Error(e, "Error reading {0}, deleting and ignoring.", _dbPath);
				_crashManager.Report(e, "json");
				return null;
			}
		}

		/// <summary>
		/// Writes the database to the internal .json file for the global database.
		/// </summary>
		private void MarshallDatabase()
		{
			var dbFolder = Path.GetDirectoryName(_dbPath);
			if (dbFolder != null && !Directory.Exists(dbFolder)) {
				_logger.Warn("Directory {0} does not exist, not writing vpdb.json.", dbFolder);
				return;
			}

			try {
				_logger.Info("Saving global database...");
				using (var sw = new StreamWriter(_dbPath))
				using (JsonWriter writer = new JsonTextWriter(sw)) {
					_serializer.Serialize(writer, Database);
				}
				_logger.Info("Saved at {0}", _dbPath);
			} catch (Exception e) {
				_logger.Error(e, "Error writing database to {0}", _dbPath);
				_crashManager.Report(e, "json");
			}
		}

		/// <summary>
		/// JSON serialization rules
		/// </summary>
		private readonly JsonSerializer _serializer = new JsonSerializer {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new SnakeCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};
	}
}
