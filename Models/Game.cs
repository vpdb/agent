using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using static System.String;
using System.Reactive.Linq;

namespace VpdbAgent.Models
{
	public class Game : ReactiveObject, IComparable<Game>
	{
		#region Read/Write Fields
		[JsonIgnoreAttribute]
		private Vpdb.Models.Release release;
		[JsonIgnoreAttribute]
		readonly ObservableAsPropertyHelper<bool> hasRelease;
		[JsonIgnoreAttribute]
		private bool exists;
		#endregion

		[DataMember]
		public string Id { get; set; }
		[DataMember]
		public string Filename { get; set; }
		[DataMember]
		public bool Enabled { get; set; } = true;

		[DataMember]
		public Vpdb.Models.Release Release
		{
			get { return release; }
			set { this.RaiseAndSetIfChanged(ref release, value); }
		}

		public bool Exists
		{
			get { return exists; }
			set { this.RaiseAndSetIfChanged(ref exists, value); }
		}
		public bool HasRelease { get { return hasRelease.Value; } }
		public long FileSize { get; set; }
		public Platform Platform { get; set; }

		/// <summary>
		/// Sets up Output Properties
		/// </summary>
		public Game()
		{
			this.WhenAnyValue(game => game.Release)
				.Select(release => release != null)
				.ToProperty(this, game => game.HasRelease, out hasRelease);
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
