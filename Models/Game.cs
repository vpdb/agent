using System;
using System.IO;
using System.Runtime.Serialization;
using ReactiveUI;
using static System.String;
using System.Reactive.Linq;
using File = System.IO.File;

namespace VpdbAgent.Models
{
	public class Game : ReactiveObject, IComparable<Game>
	{
		// read/write fields
		private string _releaseId;
		private Vpdb.Models.Release _release;
		readonly ObservableAsPropertyHelper<bool> _hasRelease;
		private bool _exists;
		private bool _syncEnabled;

		// serialized properties
		[DataMember] public string Id { get; set; }
		[DataMember] public string Filename { get; set; }
		[DataMember] public bool Enabled { get; set; } = true;
		[DataMember] public string ReleaseId {
			get { return _releaseId; }
			set { this.RaiseAndSetIfChanged(ref _releaseId, value); }
		}
		[DataMember] public bool SyncEnabled {
			get { return _syncEnabled; }
			set { this.RaiseAndSetIfChanged(ref _syncEnabled, value); }
		}

		// internal fields
		public Vpdb.Models.Release Release {
			get { return _release; }
			set { this.RaiseAndSetIfChanged(ref _release, value); }
		}
		public bool Exists {
			get { return _exists; }
			set { this.RaiseAndSetIfChanged(ref _exists, value); }
		}
		public bool HasRelease => _hasRelease.Value;
		public long FileSize { get; set; }
		public Platform Platform { get; set; }

		/// <summary>
		/// Sets up Output Properties
		/// </summary>
		public Game()
		{
			this.WhenAnyValue(game => game.ReleaseId)
				.Select(releaseId => releaseId != null)
				.ToProperty(this, game => game.HasRelease, out _hasRelease);
		}

		public Game(PinballX.Models.Game xmlGame, string tablePath, Platform platform, GlobalDatabase db) : this()
		{
			Update(platform, db);
			UpdateFromGame(xmlGame, tablePath);
		}

		internal Game Merge(PinballX.Models.Game xmlGame, string tablePath, Platform platform, GlobalDatabase db)
		{
			Update(platform, db);
			UpdateFromGame(xmlGame, tablePath);
			return this;
		}

		private void Update(Platform platform, GlobalDatabase db)
		{
			Platform = platform;
			if (ReleaseId != null) {
				Release = db.Releases[ReleaseId];
			}
		}

		private void UpdateFromGame(PinballX.Models.Game xmlGame, string tablePath)
		{
			Id = xmlGame.Description;
			Enabled = xmlGame.Enabled == null || "true".Equals(xmlGame.Enabled, StringComparison.InvariantCultureIgnoreCase);

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

		public override string ToString()
		{
			return Id;
		}
	}
}
