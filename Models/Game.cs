using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.Serialization;
using ReactiveUI;
using static System.String;
using System.Reactive.Linq;
using Splat;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Download;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Models
{
	/// <summary>
	/// Our internal Game object.
	/// 
	/// Saves itself automatically if <see cref="ReleaseId"/>, 
	/// <see cref="FileId"/> or <see cref="IsSynced"/> change.
	/// </summary>
	public class Game : ReactiveObject, IComparable<Game>
	{

		// read/write fields
		private string _releaseId;
		private string _fileId;
		private bool _exists;
		private bool _isSynced;
		private VpdbRelease _release;
		private readonly ObservableAsPropertyHelper<bool> _hasRelease;
		private readonly ObservableAsPropertyHelper<bool> _hasUpdate;
		private ObservableAsPropertyHelper<VpdbTableFile> _updatedRelease;

		/// <summary>
		/// "ID" of the game within PinballX's platform. 
		/// Comes from the <c>&lt;description&gt;</c> tag of the XML, usually something
		/// like: "Theatre of Magic (Midway 1995)"
		/// </summary>
		/// <remarks>
		/// Maps to <see cref="PinballXGame.Description"/>.
		/// </remarks>
		[DataMember] public string Id { get; set; }

		/// <summary>
		/// The entire filename with extension but without path if exists. If file was
		/// not found, contains only filename without extensions.
		/// </summary>
		/// <remarks>
		/// Maps to <see cref="PinballXGame.Filename"/>.
		/// </remarks>
		[DataMember] public string Filename { get; set; }

		/// <summary>
		/// Entire filename of the XML where the game was defined, without
		/// path.
		/// </summary>
		[DataMember] public string DatabaseFile { get; set; }

		/// <summary>
		/// True if enabled in PinballX, false otherwise.
		/// </summary>
		[DataMember] public bool Enabled { get; set; } = true;

		/// <summary>
		/// Release ID of the linked release at VPDB.
		/// </summary>
		[DataMember] public string ReleaseId { get { return _releaseId; } set { this.RaiseAndSetIfChanged(ref _releaseId, value); } }

		/// <summary>
		/// File ID of the linked file at VPDB.
		/// </summary>
		[DataMember] public string FileId { get { return _fileId; } set { this.RaiseAndSetIfChanged(ref _fileId, value); } }

		/// <summary>
		/// True if should be updated automatically, false otherwise.
		/// </summary>
		[DataMember] public bool IsSynced { get { return _isSynced; } set { this.RaiseAndSetIfChanged(ref _isSynced, value); } }

		// non-serialized props
		public VpdbRelease Release { get { return _release; } set { this.RaiseAndSetIfChanged(ref _release, value); } }
		public bool Exists { get { return _exists; } set { this.RaiseAndSetIfChanged(ref _exists, value); } }
		public bool HasRelease => _hasRelease.Value;
		public bool HasUpdate => _hasUpdate.Value;
		public VpdbTableFile UpdatedRelease => _updatedRelease?.Value;
		public long FileSize { get; set; }
		public Platform Platform { get; private set; }
		public VpdbTableFile File { get {
			return FileId != null && Release != null
				? Release.Versions.SelectMany(v => v.Files).FirstOrDefault(f => f.Reference.Id == FileId)
				: null;
		} }

		/// <summary>
		/// Sets up Output Properties
		/// </summary>
		public Game()
		{
			var downloadManager = Locator.Current.GetService<IDownloadManager>();

			// update HasRelease
			this.WhenAnyValue(game => game.ReleaseId)
				.Select(releaseId => releaseId != null)
				.ToProperty(this, game => game.HasRelease, out _hasRelease);

			// watch versions and release for updates
			this.WhenAnyValue(g => g.Release).Subscribe(release => {
				if (release == null) {
					return;
				}
				var versionsUpdated = release.Versions.Changed.Select(_ => Unit.Default);
				var releaseOrFileUpdated = this.WhenAnyValue(game => game.Release, game => game.FileId).Select(_ => Unit.Default);
				versionsUpdated.Merge(releaseOrFileUpdated)
					.Select(x =>
					{
						return downloadManager.FindLatestFile(Release, File);
					})
					.ToProperty(this, game => game.UpdatedRelease, out _updatedRelease);

				releaseOrFileUpdated.Subscribe(_ => {
					Console.WriteLine("Release or file updated!");
				});

				versionsUpdated.Subscribe(_ => {
					Console.WriteLine("Versions changed!");
				});


			});



			// watch updated file for hasUpdate
			this.WhenAnyValue(game => game.UpdatedRelease)
				.Select(update => update != null)
				.ToProperty(this, game => game.HasUpdate, out _hasUpdate);

			// download game when available
			//this.WhenAnyValue(game => game.UpdatedRelease)
			//	.Where(update => update != null && IsSynced)
			//	.Subscribe(update => { downloadManager.DownloadRelease(ReleaseId, File); });

			this.WhenAnyValue(game => game.UpdatedRelease)
				.Where(update => update != null && IsSynced)
				.Subscribe(update => {
					var from = Release.Versions.FirstOrDefault(v => v.Files.Contains(v.Files.FirstOrDefault(f => f.Reference.Id == FileId)));
					var to = Release.Versions.FirstOrDefault(v => v.Files.Contains(update));
					Console.WriteLine("Updating from v{0} to v{1}.", from.Name, to.Name);
				});
		}

		public Game(PinballXGame xmlGame, Platform platform, GlobalDatabase db) : this()
		{
			Update(platform, db);
			UpdateFromGame(xmlGame, platform.TablePath);
		}

		internal Game Merge(PinballXGame xmlGame, Platform platform, GlobalDatabase db)
		{
			Update(platform, db);
			UpdateFromGame(xmlGame, platform.TablePath);
			return this;
		}

		private void Update(Platform platform, GlobalDatabase db)
		{
			Platform = platform;

			// save to disk if these attributes change
			this.WhenAny(g => g.ReleaseId, g => g.FileId, g => g.IsSynced, (rid, fid, s) => Unit.Default)
				.Subscribe(Platform.GamePropertyChanged);

			// link release id to release object
			if (ReleaseId != null && db.Releases.ContainsKey(ReleaseId)) {
				Release = db.Releases[ReleaseId];
			}
		}

		private void UpdateFromGame(PinballXGame xmlGame, string tablePath)
		{
			Id = xmlGame.Description;
			Enabled = xmlGame.Enabled == null || "true".Equals(xmlGame.Enabled, StringComparison.InvariantCultureIgnoreCase);
			DatabaseFile = xmlGame.DatabaseFile;

			var oldFilename = Filename;

			if (System.IO.File.Exists(tablePath + @"\" + xmlGame.Filename + ".vpt")) {
				Filename = xmlGame.Filename + ".vpt";
				FileSize = new FileInfo(tablePath + @"\" + xmlGame.Filename + ".vpt").Length;
				Exists = true;
			} else if (System.IO.File.Exists(tablePath + @"\" + xmlGame.Filename + ".vpx")) {
				Filename = xmlGame.Filename + ".vpx";
				FileSize = new FileInfo(tablePath + @"\" + xmlGame.Filename + ".vpx").Length;
				Exists = true;
			} else {
				Filename = xmlGame.Filename;
				Exists = false;
			}

			// if filename has changed, unlink.
			if (oldFilename != Filename) {
				FileId = null;
				ReleaseId = null;
				Release = null;
			}
		}

		public int CompareTo(Game other)
		{
			return other == null ? 1 : Compare(Id, other.Id, StringComparison.Ordinal);
		}

		public bool Equals(Game game)
		{
			return game?.Id != null && game.Id.Equals(Id);
		}

		public bool Equals(PinballXGame game)
		{
			return game?.Description != null && game.Description.Equals(Id);
		}

		public override string ToString()
		{
			return Id;
		}
	}
}
