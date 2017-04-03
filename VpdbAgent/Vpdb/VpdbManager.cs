using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using LiteDB;
using VpdbAgent.Application;
using VpdbAgent.Vpdb.Models;
using ILogger = NLog.ILogger;

namespace VpdbAgent.Vpdb
{
	/// <summary>
	/// Manages data from and to VPDB while caching as much as possible.
	/// </summary>
	public interface IVpdbManager : IDisposable
	{
		/// <summary>
		/// Init LiteDB, run when settings are initialized.
		/// </summary>
		/// 
		/// <returns>This instance</returns>
		IVpdbManager Initialize();

		/// <summary>
		/// Returns a release
		/// </summary>
		/// <param name="releaseId">ID of the release</param>
		/// <returns>Full release object</returns>
		IObservable<VpdbRelease> GetRelease(string releaseId);
	}

	public class VpdbManager : IVpdbManager
	{
		// dependencies
		private readonly IVpdbClient _vpdbClient;
		private readonly ISettingsManager _settingsManager;
		private readonly IThreadManager _threadManager;
		private readonly ILogger _logger;

		// DB stuff
		private LiteDatabase _db;
		private readonly BsonMapper _mapper = new BsonMapper();

		// table names
		const string TableGames = "games";
		const string TableReleases = "releases";
		const string TableFiles = "files";
		const string TableUsers = "users";

		// collections
		private LiteCollection<VpdbGame> _games;
		private LiteCollection<VpdbRelease> _releases;
		private LiteCollection<VpdbFile> _files;
		private LiteCollection<VpdbUser> _users;

		public VpdbManager(IVpdbClient vpdbClient, ISettingsManager settingsManager, IThreadManager threadManager, ILogger logger)
		{
			_vpdbClient = vpdbClient;
			_settingsManager = settingsManager;
			_threadManager = threadManager;
			_logger = logger;
		}

		public IVpdbManager Initialize()
		{
			var dbPath = Path.Combine(_settingsManager.Settings.PinballXFolder, @"Databases\vpdb-agent.db");
			_db = new LiteDatabase(dbPath, _mapper);
			_games = _db.GetCollection<VpdbGame>(TableGames);
			_releases = _db.GetCollection<VpdbRelease>(TableReleases);
			_files = _db.GetCollection<VpdbFile>(TableFiles);
			_users = _db.GetCollection<VpdbUser>(TableUsers);

			_logger.Info("LiteDB initialized at {0}.", dbPath);
			return this;
		}

		public IObservable<VpdbRelease> GetRelease(string releaseId)
		{
			var release = _releases.Include(x => x.Game).FindById(releaseId);
			if (release == null) {
				return _vpdbClient.Api.GetRelease(releaseId).Do(Save);
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
				});
			});
			release.Authors.ToList().ForEach(author => {
				author.User = _users.FindById(author.User.Id);
			});
			return Observable.Return(release);
		}

		/// <summary>
		/// Updates or creates a given release.
		/// </summary>
		/// <param name="release">Release to save</param>
		private void Save(VpdbRelease release)
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
				});
			});
			release.Authors.ToList().ForEach(author => {
				_users.Upsert(author.User);
			});

			_releases.Upsert(release);
		}

		public void Dispose()
		{
			_db.Dispose();
		}
	}
}
