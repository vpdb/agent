using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using JetBrains.Annotations;
using LiteDB;
using NLog;
using ReactiveUI;
using VpdbAgent.Application;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Download;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Data
{
	/// <summary>
	/// Manages the global stuff we're saving to disk.
	/// 
	/// This is the LiteDB implementation.
	/// </summary>
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

		private LiteDatabase _db;
		private readonly BsonMapper _mapper = BsonMapper.Global;
		private readonly BehaviorSubject<bool> _initialized = new BehaviorSubject<bool>(false);
		private readonly Subject<Unit> _save = new Subject<Unit>();

		// collections
		private LiteCollection<Job> _jobs;
		private LiteCollection<Message> _messages;
		private LiteCollection<VpdbRelease> _releases;
		private LiteCollection<VpdbFile> _files;

		// collection names
		private const string Jobs = "jobs";
		private const string Messages = "messages";
		private const string VpdbFiles = "vpdb_files";
		private const string VpdbGames = "vpdb_games";
		private const string VpdbReleases = "vpdb_releases";
		private const string VpdbUsers = "vpdb_users";

		public DatabaseManager(ISettingsManager settingsManager, CrashManager crashManager, ILogger logger)
		{
			_settingsManager = settingsManager;
			_crashManager = crashManager;
			_logger = logger;
		}

		public IDatabaseManager Initialize()
		{

			// relational mappings
			_mapper.Entity<VpdbGame>()
				.Id(g => g.Id, false)
				.DbRef(g => g.Backglass, VpdbFiles)
				.DbRef(g => g.Logo, VpdbFiles);
			_mapper.Entity<VpdbRelease>()
				.Id(r => r.Id, false)
				.DbRef(r => r.Game, VpdbGames);
			_mapper.Entity<VpdbTableFile>()
				.DbRef(f => f.Reference, VpdbFiles)
				.DbRef(f => f.PlayfieldImage, VpdbFiles)
				.DbRef(f => f.PlayfieldVideo, VpdbFiles);
			_mapper.Entity<VpdbAuthor>()
				.DbRef(r => r.User, VpdbUsers);
			_mapper.Entity<VpdbFile>()
				.Id(f => f.Id, false);

			// db & collections
			_db = new LiteDatabase(Path.Combine(_settingsManager.Settings.PinballXFolder, @"Databases\vpdb.db"));
			_jobs = _db.GetCollection<Job>(Jobs);
			_messages = _db.GetCollection<Message>(Messages);
			_releases = _db.GetCollection<VpdbRelease>(VpdbReleases);
			_files = _db.GetCollection<VpdbFile>(VpdbFiles);

			_logger.Info("Global database with {0} release(s) loaded.", _releases.Count());
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
			return _releases.FindOne(r => r.Id == releaseId);
		}

		public VpdbVersion GetVersion(string releaseId, string fileId)
		{
			var release = _releases.FindOne(r => r.Id == releaseId);
			if (release == null) {
				_logger.Warn("Release with ID \"{0}\" not found when retrieving version.", releaseId);
				return null;
			}

			// todo add map to make it fast
			return release.Versions
				.FirstOrDefault(v => v.Files.Contains(v.Files.FirstOrDefault(f => f.Reference.Id == fileId)));
		}

		public VpdbTableFile GetTableFile(string releaseId, string fileId)
		{
			var release = _releases.FindOne(r => r.Id == releaseId);
			if (release == null) {
				_logger.Warn("Release with ID \"{0}\" not found when retrieving table file.", releaseId);
				return null;
			}
			// todo add map to make it fast
			return release.Versions
					.SelectMany(v => v.Files)
					.FirstOrDefault(f => f.Reference.Id == fileId);
		}

		public VpdbFile GetFile(string fileId)
		{
			return _files.FindOne(f => f.Id == fileId);
		}

		public void AddOrUpdateRelease(VpdbRelease release)
		{
			var dirtyRelease = _releases.FindOne(r => r.Id == release.Id);
			if (dirtyRelease == null) {
				_logger.Info("Adding new release data for release {0} ({1})", release.Id, release.Name);
				_releases.Insert(release);

			} else {
				_logger.Info("Updating release data of release {0} ({1})", release.Id, release.Name);
				dirtyRelease.Update(release);
				_releases.Update(dirtyRelease);
			}
		}

		public void AddOrReplaceFile(VpdbFile file)
		{
			var dirtyFile = _files.FindOne(f => f.Id == file.Id);
			if (dirtyFile == null) {
				_files.Insert(file);

			} else {
				dirtyFile.Update(file);
				_files.Update(dirtyFile);
			}
		}

		public void AddJob(Job job)
		{
			_jobs.Insert(job);
		}

		public ReactiveList<Job> GetJobs()
		{
			return new ReactiveList<Job>(_jobs.FindAll());
		}

		public void RemoveJob(Job job)
		{
			//_jobs.Delete()
		}

		public IReactiveList<Message> GetMessages()
		{
			return new ReactiveList<Message>(_messages.FindAll());
		}

		public void Log(Message msg)
		{
			_messages.Insert(msg);
		}

		public void ClearLog()
		{
			_messages.Delete(x => true);
		}
	}
}
