using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.Serialization;
using ReactiveUI;
using static System.String;
using System.Reactive.Linq;
using Splat;
using VpdbAgent.Application;
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

		/// <summary>
		/// File ID of the linked file at VPDB.
		/// </summary>
		[DataMember]
		public string PreviousFileId { get { return _previousFileId; } set { this.RaiseAndSetIfChanged(ref _previousFileId, value); } }

		/// <summary>
		/// The table script as it was saved back after patching. If null, the script
		/// either hasn't previously been updated, there was no previous version
		/// or patching resulted in a conflict.
		/// </summary>
		[DataMember] public string PatchedTableScript { get { return _patchedTableScript; } set { this.RaiseAndSetIfChanged(ref _patchedTableScript, value); } }

		// dependencies
		private static readonly IDatabaseManager DatabaseManager = Locator.Current.GetService<IDatabaseManager>();
		
		// object lookups
		public VpdbRelease Release => _release.Value;
		public VpdbVersion Version => _version.Value;
		public VpdbTableFile File => _file.Value;
		private readonly ObservableAsPropertyHelper<VpdbRelease> _release;
		private readonly ObservableAsPropertyHelper<VpdbVersion> _version;
		private readonly ObservableAsPropertyHelper<VpdbTableFile> _file;

		// read/write fields
		private string _releaseId;
		private string _fileId;
		private bool _exists;
		private bool _isSynced;
		private string _patchedTableScript;
		private string _previousFileId;
		private readonly ObservableAsPropertyHelper<bool> _hasRelease;
		private readonly ObservableAsPropertyHelper<bool> _hasUpdate;
		private ObservableAsPropertyHelper<VpdbTableFile> _updatedRelease;

		// non-serialized props
		public bool Exists { get { return _exists; } set { this.RaiseAndSetIfChanged(ref _exists, value); } }
		public bool HasRelease => _hasRelease.Value;
		public bool HasUpdate => _hasUpdate.Value;
		public VpdbTableFile UpdatedRelease => _updatedRelease?.Value;
		public long FileSize { get; set; }
		public Platform Platform { get; private set; }

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

			// setup output props that link to objects
			this.WhenAnyValue(x => x.ReleaseId).Select(releaseId => DatabaseManager.GetRelease(ReleaseId)).ToProperty(this, x => x.Release, out _release);
			this.WhenAnyValue(x => x.FileId).Select(fileId => DatabaseManager.GetVersion(ReleaseId, fileId)).ToProperty(this, x => x.Version, out _version);
			this.WhenAnyValue(x => x.FileId).Select(fileId => DatabaseManager.GetTableFile(ReleaseId, fileId)).ToProperty(this, x => x.File, out _file);

			// watch versions and release for updates
			this.WhenAnyValue(g => g.Release).Subscribe(release => {
				if (release == null) {
					return;
				}
				var versionsUpdated = release.Versions.Changed.Select(_ => Unit.Default);
				var releaseOrFileUpdated = this.WhenAnyValue(g => g.FileId).Select(_ => Unit.Default);
				versionsUpdated.Merge(releaseOrFileUpdated)
					.Select(x => downloadManager.FindLatestFile(Release, FileId))
					.ToProperty(this, game => game.UpdatedRelease, out _updatedRelease);
			});

			// watch updated file for hasUpdate
			this.WhenAnyValue(game => game.UpdatedRelease)
				.Select(update => update != null)
				.ToProperty(this, game => game.HasUpdate, out _hasUpdate);

			// download game when available
			this.WhenAnyValue(game => game.UpdatedRelease)
				.Where(update => update != null && IsSynced)
				.Subscribe(update => {
					downloadManager.DownloadRelease(ReleaseId, FileId);
				});
		}

		public Game(PinballXGame xmlGame, Platform platform) : this()
		{
			Update(platform);
			UpdateFromGame(xmlGame, platform.TablePath);
		}

		public Game Merge(PinballXGame xmlGame, Platform platform)
		{
			Update(platform);
			UpdateFromGame(xmlGame, platform.TablePath);
			return this;
		}

		private void Update(Platform platform)
		{
			Platform = platform;

			// save to disk if these attributes change
			this.WhenAny(g => g.ReleaseId, g => g.FileId, g => g.IsSynced, g => g.PreviousFileId, g => g.PatchedTableScript, (rid, fid, s, pfid, script) => Unit.Default)
				.Subscribe(Platform.GamePropertyChanged);
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
