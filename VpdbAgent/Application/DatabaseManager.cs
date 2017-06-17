using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using JetBrains.Annotations;
using LiteDB;
using NLog;
using ReactiveUI;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Download;
using VpdbAgent.Vpdb.Models;

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
	public interface IDatabaseManager : IDisposable
	{
		IObservable<bool> Initialized { get; }

		/// <summary>
		/// Read data, run when settings are initialized.
		/// </summary>
		/// <returns></returns>
		IDatabaseManager Initialize();

		/// <summary>
		/// Returns the entire release object for a given release ID.
		/// </summary>
		/// <param name="releaseId">Release ID</param>
		/// <returns>Release or null if release not in database</returns>
		[CanBeNull] VpdbRelease GetRelease(string releaseId);

		/// <summary>
		/// Updates or creates a given release.
		/// </summary>
		/// <param name="release">Release to save</param>
		void SaveRelease(VpdbRelease release);

		/// <summary>
		/// Updates or creates a given file.
		/// </summary>
		/// <param name="file">File to save</param>
		void SaveFile(VpdbFile file);

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
		[CanBeNull] VpdbTableFile GetTableFile(string releaseId, string fileId);

		/// <summary>
		/// Returns the file object for a given file ID.
		/// </summary>
		/// <param name="fileId">File ID</param>
		/// <returns>File or null if not found</returns>
		[CanBeNull] VpdbFile GetFile(string fileId);

		/// <summary>
		/// Returns the game for a given game ID.
		/// </summary>
		/// <param name="gameId">Game ID</param>
		/// <returns>Game or null if not found</returns>
		[CanBeNull] VpdbGame GetGame(string gameId);

		/// <summary>
		/// Updates or creates a given game.
		/// </summary>
		/// <param name="game">Game to save</param>
		void SaveGame(VpdbGame game);

		/// <summary>
		/// Adds a new download job to the database.
		/// </summary>
		/// <param name="job">Job to add</param>
		void AddJob(Job job);

		/// <summary>
		/// Returns all jobs in the database.
		/// </summary>
		/// <returns>Download jobs</returns>
		List<Job> GetJobs();

		void SaveJob(Job job);

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

		// DB stuff
		private LiteDatabase _db;
		private readonly BsonMapper _mapper = new BsonMapper();

		// table names
		public const string TableGames = "games";
		public const string TableReleases = "releases";
		public const string TableFiles = "files";
		public const string TableBuilds = "builds";
		public const string TableUsers = "users";
		public const string TableJobs = "jobs";
		public const string TableMessages = "messages";

		// collections
		private LiteCollection<VpdbGame> _games;
		private LiteCollection<VpdbRelease> _releases;
		private LiteCollection<VpdbFile> _files;
		private LiteCollection<VpdbUser> _users;
		private LiteCollection<VpdbTableFile.VpdbCompatibility> _builds;
		private LiteCollection<Job> _jobs;
		private LiteCollection<Message> _messages;

		// props
		public IObservable<bool> Initialized => _initialized;
		private string _dbPath;
		private readonly BehaviorSubject<bool> _initialized = new BehaviorSubject<bool>(false);
		private ReactiveList<Message> _messageList;

		public DatabaseManager(ISettingsManager settingsManager, CrashManager crashManager, ILogger logger)
		{
			_settingsManager = settingsManager;
			_crashManager = crashManager;
			_logger = logger;
		}

		public IDatabaseManager Initialize()
		{
			_dbPath = Path.Combine(_settingsManager.Settings.PinballXFolder, @"Databases\vpdb-agent.db");
			_db = new LiteDatabase(_dbPath, _mapper);

			_games = _db.GetCollection<VpdbGame>(TableGames);
			_releases = _db.GetCollection<VpdbRelease>(TableReleases);
			_files = _db.GetCollection<VpdbFile>(TableFiles);
			_builds = _db.GetCollection<VpdbTableFile.VpdbCompatibility>(TableBuilds); 
			_users = _db.GetCollection<VpdbUser>(TableUsers);
			_jobs = _db.GetCollection<Job>(TableJobs);
			_messages = _db.GetCollection<Message>(TableMessages);

			_mapper.UseLowerCaseDelimiter('_');
			_mapper.Entity<VpdbRelease>().Ignore(x => x.Changing).Ignore(x => x.Changed).Ignore(x => x.ThrownExceptions);
			_mapper.Entity<VpdbVersion>().Ignore(x => x.Changing).Ignore(x => x.Changed).Ignore(x => x.ThrownExceptions);
			_mapper.Entity<VpdbTableFile>().Ignore(x => x.Changing).Ignore(x => x.Changed).Ignore(x => x.ThrownExceptions);
			_mapper.Entity<VpdbThumb>().Ignore(x => x.Changing).Ignore(x => x.Changed).Ignore(x => x.ThrownExceptions);
			_mapper.Entity<VpdbImage>().Ignore(x => x.Changing).Ignore(x => x.Changed).Ignore(x => x.ThrownExceptions);
			_mapper.Entity<Job>().Ignore(x => x.Changing).Ignore(x => x.Changed).Ignore(x => x.ThrownExceptions);

			_logger.Info("LiteDB initialized at {0}.", _dbPath);
			_initialized.OnNext(true);
			return this;
		}

		#region VPDB
		public VpdbRelease GetRelease(string releaseId)
		{
			var release = _releases.Include(x => x.Game).FindById(releaseId);
			if (release == null) {
				return null;
			}

			if (release.Game.Backglass != null) {
				release.Game.Backglass = _files.FindById(release.Game.Backglass.Id);
			}
			if (release.Game.Logo != null) {
				release.Game.Logo = _files.FindById(release.Game.Logo.Id);
			}
			release.Versions.ToList().ForEach(version => {
				version.Files.ToList().ForEach(file => {
					file.Reference = _files.FindById(file.Reference.Id);
					file.PlayfieldImage = _files.FindById(file.PlayfieldImage.Id);
					if (file.PlayfieldVideo != null) {
						file.PlayfieldVideo = _files.FindById(file.PlayfieldVideo.Id);
					}
					file.Compatibility = file.Compatibility.Select(build => _builds.FindById(build.Id)).ToList();
				});
			});
			release.Authors.ToList().ForEach(author => {
				author.User = _users.FindById(author.User.Id);
			});
			return release;
		}

		public void SaveFile(VpdbFile file)
		{
			_files.Upsert(file);
		}

		public void SaveRelease(VpdbRelease release)
		{
			_games.Upsert(release.Game);
			if (release.Game.Backglass != null) {
				_files.Upsert(release.Game.Backglass);
			}
			if (release.Game.Logo != null) {
				_files.Upsert(release.Game.Logo);
			}
			release.Versions.ToList().ForEach(version => {
				version.Files.ToList().ForEach(file => {
					_files.Upsert(file.Reference);
					_files.Upsert(file.PlayfieldImage);
					if (file.PlayfieldVideo != null) {
						_files.Upsert(file.PlayfieldVideo);
					}
					file.Compatibility.ToList().ForEach(build => _builds.Upsert(build));
				});
			});
			release.Authors.ToList().ForEach(author => {
				_users.Upsert(author.User);
			});
			_releases.Upsert(release);
		}

		public VpdbVersion GetVersion(string releaseId, string fileId)
		{
			if (releaseId == null) {
				return null;
			}
			var release = _releases.FindById(releaseId);
			if (release == null) {
				_logger.Warn("Release with ID \"{0}\" not found when retrieving version.", releaseId);
				return null;
			}

			// todo add map to make it fast
			return release.Versions.FirstOrDefault(v => v.Files.Contains(v.Files.FirstOrDefault(f => f.Reference.Id == fileId)));
		}

		public VpdbTableFile GetTableFile(string releaseId, string fileId)
		{
			if (releaseId == null) {
				return null;
			}
			var release = _releases.FindById(releaseId);
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
			return _files.FindById(fileId);
		}

		public VpdbGame GetGame(string gameId)
		{
			return _games
				.Include(g => g.Backglass)
				.Include(g => g.Logo)
				.FindById(gameId);
		}

		public void SaveGame(VpdbGame game)
		{
			_games.Upsert(game);
			if (game.Backglass != null) {
				_files.Upsert(game.Backglass);
			}
			if (game.Logo != null) {
				_files.Upsert(game.Logo);
			}
		}
		#endregion

		#region Jobs
		public void AddJob(Job job)
		{
			if (GetRelease(job.Release.Id) == null) {
				SaveRelease(job.Release);
			}
			if (GetFile(job.File.Id) == null) {
				SaveFile(job.File);
			}
			SaveRelease(job.Release);
			_jobs.Insert(job);
		}

		public List<Job> GetJobs()
		{
			var jobs = _jobs
				.Include(j => j.File)
				.FindAll()
				.ToList();
			jobs.ForEach(job => job.Release = GetRelease(job.Release.Id));
			return jobs;
		}

		public void SaveJob(Job job)
		{
			_jobs.Update(job);
		}

		public void RemoveJob(Job job)
		{
			_jobs.Delete(job.Id);
		}
		#endregion

		#region Messages
		public IReactiveList<Message> GetMessages()
		{
			return _messageList ?? (_messageList = new ReactiveList<Message>(_messages.FindAll()));
		}

		public void Log(Message msg)
		{
			_messages.Insert(msg);
			_messageList.Add(msg);
		}

		public void ClearLog()
		{
			_messages.Delete(Query.All());
			_messageList.Clear();
		}
		#endregion

		public void Dispose()
		{
			_db.Dispose();
		}
	}
}
