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

namespace VpdbAgent.Models
{
	public class Game : ReactiveObject, IComparable<Game>
	{

		[DataMember]
		public string Id { get; set; }
		[DataMember]
		public string Filename { get; set; }
		[DataMember]
		public bool Enabled { get; set; } = true;

		[DataMember]
		public Vpdb.Models.Release Release { get; set; }


		[JsonIgnoreAttribute]
		public bool Exists { get; set; }

		[JsonIgnoreAttribute]
		public bool HasRelease => Release != null;

		[JsonIgnoreAttribute]
		public long FileSize { get; set; }

		[JsonIgnoreAttribute]
		public Platform Platform { get; set; }

		public Game() { }

		public Game(PinballX.Models.Game xmlGame, string tablePath, Platform platform)
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
