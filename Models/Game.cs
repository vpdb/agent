using System;
using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Runtime.Serialization;
using ReactiveUI;
using static System.String;
using System.Reactive.Linq;
using File = System.IO.File;

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
		private Vpdb.Models.Release _release;
		readonly ObservableAsPropertyHelper<bool> _hasRelease;
		private bool _exists;
		private bool _isSynced;

		// serialized properties
		[DataMember] public string Id { get; set; }
		[DataMember] public string Filename { get; set; }
		[DataMember] public string DatabaseFile { get; set; }
		[DataMember] public bool Enabled { get; set; } = true;
		[DataMember] public string ReleaseId { get { return _releaseId; } set { this.RaiseAndSetIfChanged(ref _releaseId, value); } }
		[DataMember] public string FileId { get { return _fileId; } set { this.RaiseAndSetIfChanged(ref _fileId, value); } }
		[DataMember] public bool IsSynced { get { return _isSynced; } set { this.RaiseAndSetIfChanged(ref _isSynced, value); } }

		// internal fields
		public Vpdb.Models.Release Release { get { return _release; } set { this.RaiseAndSetIfChanged(ref _release, value); } }
		public bool Exists { get { return _exists; } set { this.RaiseAndSetIfChanged(ref _exists, value); } }
		public bool HasRelease => _hasRelease.Value;
		public long FileSize { get; set; }
		public Platform Platform { get; private set; }

		/// <summary>
		/// Sets up Output Properties
		/// </summary>
		public Game()
		{
			// update HasRelease
			this.WhenAnyValue(game => game.ReleaseId)
				.Select(releaseId => releaseId != null)
				.ToProperty(this, game => game.HasRelease, out _hasRelease);
		}

		public Game(PinballX.Models.Game xmlGame, Platform platform, GlobalDatabase db) : this()
		{
			Update(platform, db);
			UpdateFromGame(xmlGame, platform.TablePath);
		}

		internal Game Merge(PinballX.Models.Game xmlGame, Platform platform, GlobalDatabase db)
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

		private void UpdateFromGame(PinballX.Models.Game xmlGame, string tablePath)
		{
			Id = xmlGame.Description;
			Enabled = xmlGame.Enabled == null || "true".Equals(xmlGame.Enabled, StringComparison.InvariantCultureIgnoreCase);
			DatabaseFile = xmlGame.DatabaseFile;

			if (File.Exists(tablePath + @"\" + xmlGame.Filename + ".vpt")) {
				Filename = xmlGame.Filename + ".vpt";
				FileSize = new FileInfo(tablePath + @"\" + xmlGame.Filename + ".vpt").Length;
				Exists = true;
			} else if (File.Exists(tablePath + @"\" + xmlGame.Filename + ".vpx")) {
				Filename = xmlGame.Filename + ".vpx";
				FileSize = new FileInfo(tablePath + @"\" + xmlGame.Filename + ".vpx").Length;
				Exists = true;
			} else {
				Filename = xmlGame.Filename;
				Exists = false;
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

		public bool Equals(PinballX.Models.Game game)
		{
			return game?.Description != null && game.Description.Equals(Id);
		}

		public override string ToString()
		{
			return Id;
		}
	}
}
