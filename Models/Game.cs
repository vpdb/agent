using System;
using System.IO;
using System.Runtime.Serialization;
using ReactiveUI;
using static System.String;
using System.Reactive.Linq;

namespace VpdbAgent.Models
{
	public class Game : ReactiveObject, IComparable<Game>
	{
		// read/write fields
		private Vpdb.Models.Release _release;
		readonly ObservableAsPropertyHelper<bool> _hasRelease;
		private bool _exists;

		// serialized properties
		[DataMember] public string Id { get; set; }
		[DataMember] public string Filename { get; set; }
		[DataMember] public bool Enabled { get; set; } = true;
		[DataMember] public Vpdb.Models.Release Release {
			get { return _release; }
			set { this.RaiseAndSetIfChanged(ref _release, value); }
		}

		// internal fields
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
			this.WhenAnyValue(game => game.Release)
				.Select(release => release != null)
				.ToProperty(this, game => game.HasRelease, out _hasRelease);
		}

		public Game(PinballX.Models.Game xmlGame, string tablePath, Platform platform) : this()
		{
			Platform = platform;
			UpdateFromGame(xmlGame, tablePath);
		}

		internal Game Merge(PinballX.Models.Game xmlGame, string tablePath, Platform platform)
		{
			Platform = platform;
			UpdateFromGame(xmlGame, tablePath);
			return this;
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
	}
}
