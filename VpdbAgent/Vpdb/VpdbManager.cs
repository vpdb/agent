using System;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Forms;
using LiteDB;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.PinballX;
using VpdbAgent.Vpdb.Models;
using ILogger = NLog.ILogger;

namespace VpdbAgent.Vpdb
{
	public interface IVpdbManager
	{
		IObservable<VpdbRelease> GetRelease(string releaseId);
	}

	public class VpdbManager : IVpdbManager
	{
		// dependencies
		private readonly IVpdbClient _vpdbClient;
		private readonly IMenuManager _menuManager;
		private readonly IThreadManager _threadManager;
		private readonly ILogger _logger;

		private readonly BsonMapper _mapper = new BsonMapper();
		private readonly LiteDatabase _db;

		const string TableGames = "games";
		const string TableReleases = "releases";
		const string TableFiles = "files";
		const string TableUsers = "users";

		private readonly LiteCollection<VpdbGame> _games;
		private readonly LiteCollection<VpdbRelease> _releases;
		private readonly LiteCollection<VpdbFile> _files;
		private readonly LiteCollection<VpdbUser> _users;

		public VpdbManager(IVpdbClient vpdbClient, IMenuManager menuManager, IThreadManager threadManager, ILogger logger)
		{
			_vpdbClient = vpdbClient;
			_menuManager = menuManager;
			_threadManager = threadManager;
			_logger = logger;

			_db = new LiteDatabase("Vpdb.db", _mapper);

			_games = _db.GetCollection<VpdbGame>(TableGames);
			_releases = _db.GetCollection<VpdbRelease>(TableReleases);
			_files = _db.GetCollection<VpdbFile>(TableFiles);
			_users = _db.GetCollection<VpdbUser>(TableUsers);
		}

		public IObservable<VpdbRelease> GetRelease(string releaseId)
		{
			var release = _releases.Include(x => x.Game).FindById(releaseId);
			if (release != null) {
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

			return _vpdbClient.Api.GetRelease(releaseId).Do(Save);
		}

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
	}
}
