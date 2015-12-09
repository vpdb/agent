﻿using System;
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
		private Release _release;
		private readonly ObservableAsPropertyHelper<bool> _hasRelease;
		private readonly ObservableAsPropertyHelper<bool> _hasUpdate;
		private readonly ObservableAsPropertyHelper<TableFile> _updatedRelease;

		// serialized properties
		[DataMember] public string Id { get; set; }
		[DataMember] public string Filename { get; set; }
		[DataMember] public string DatabaseFile { get; set; }
		[DataMember] public bool Enabled { get; set; } = true;
		[DataMember] public string ReleaseId { get { return _releaseId; } set { this.RaiseAndSetIfChanged(ref _releaseId, value); } }
		[DataMember] public string FileId { get { return _fileId; } set { this.RaiseAndSetIfChanged(ref _fileId, value); } }
		[DataMember] public bool IsSynced { get { return _isSynced; } set { this.RaiseAndSetIfChanged(ref _isSynced, value); } }

		// non-serialized props
		public Release Release { get { return _release; } set { this.RaiseAndSetIfChanged(ref _release, value); } }
		public bool Exists { get { return _exists; } set { this.RaiseAndSetIfChanged(ref _exists, value); } }
		public bool HasRelease => _hasRelease.Value;
		public bool HasUpdate => _hasUpdate.Value;
		public TableFile UpdatedRelease => _updatedRelease.Value;
		public long FileSize { get; set; }
		public Platform Platform { get; private set; }
		public TableFile File { get {
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

			this.WhenAnyValue(game => game.Release, game => game.FileId, (release, fileId) => new { game = this, release })
				.Select(x => downloadManager.FindLatestFile(x.release, x.game.File))
				.ToProperty(this, game => game.UpdatedRelease, out _updatedRelease);

			this.WhenAnyValue(game => game.UpdatedRelease)
				.Select(update => update != null)
				.ToProperty(this, game => game.HasUpdate, out _hasUpdate);
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

		/// <summary>
		/// Checks if the given release has a newer version than the one
		/// referenced in the game.
		/// </summary>
		/// <param name="release">Freshly obtained release from VPDB</param>
		/// <returns>The newer file if available, null if no update available</returns>
		public TableFile FindUpdate2(Release release)
		{
			if (FileId == null || release == null) {
				return null;
			}

			// for now, only orientation is checked. todo add more configurable attributes.
			var files = release.Versions
				.SelectMany(version => version.Files)
				.Where(file => file.Flavor.Orientation == Flavor.OrientationValue.FS)
				.ToList();

			files.Sort((a, b) => b.ReleasedAt.CompareTo(a.ReleasedAt));

			var latestFile = files[0];
			var updatedRelease = !latestFile.Reference.Id.Equals(FileId) ? latestFile : null;
			return updatedRelease;
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
