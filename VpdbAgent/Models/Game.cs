using System;
using System.IO;
using System.Reactive;
using System.Runtime.Serialization;
using ReactiveUI;
using static System.String;
using System.Reactive.Linq;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
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
	[Obsolete("Use AggregatedGame")]
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
		private readonly IDatabaseManager _databaseManager;
		private readonly IFile _file;
		
		// object lookups
		public VpdbRelease Release => _release.Value;
		public VpdbVersion Version => _version.Value;
		public VpdbTableFile File => _tableFile.Value;
		private readonly ObservableAsPropertyHelper<VpdbRelease> _release;
		private readonly ObservableAsPropertyHelper<VpdbVersion> _version;
		private readonly ObservableAsPropertyHelper<VpdbTableFile> _tableFile;

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
		public Game(IDependencyResolver resolver)
		{
			var downloadManager = resolver.GetService<IDownloadManager>();
			_databaseManager = resolver.GetService<IDatabaseManager>();
			_file = resolver.GetService<IFile>();

			// update HasRelease
			this.WhenAnyValue(game => game.ReleaseId)
				.Select(releaseId => releaseId != null)
				.ToProperty(this, game => game.HasRelease, out _hasRelease);

			// setup output props that link to objects
			this.WhenAnyValue(x => x.ReleaseId).Select(releaseId => _databaseManager.GetRelease(ReleaseId)).ToProperty(this, x => x.Release, out _release);
			this.WhenAnyValue(x => x.FileId).Select(fileId => _databaseManager.GetVersion(ReleaseId, fileId)).ToProperty(this, x => x.Version, out _version);
			this.WhenAnyValue(x => x.FileId).Select(fileId => _databaseManager.GetTableFile(ReleaseId, fileId)).ToProperty(this, x => x.File, out _tableFile);

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

		public Game() : this(Locator.Current) { }


		private void UpdateFromGame(PinballXGame xmlGame, string tablePath)
		{
			Id = xmlGame.Description;
			Enabled = xmlGame.Enabled == null || "true".Equals(xmlGame.Enabled, StringComparison.InvariantCultureIgnoreCase);
			DatabaseFile = xmlGame.DatabaseFile;

			var oldFilename = Filename;
			var vptPath = Path.Combine(tablePath, xmlGame.Filename + ".vpt");
			var vpxPath = Path.Combine(tablePath, xmlGame.Filename + ".vpx");
			if (_file.Exists(vptPath)) {
				Filename = Path.GetFileName(vptPath);
				FileSize = _file.FileSize(vptPath);
				Exists = true;
			} else if (_file.Exists(vpxPath)) {
				Filename = Path.GetFileName(vpxPath);
				FileSize = _file.FileSize(vpxPath);
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
