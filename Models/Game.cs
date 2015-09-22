using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Models
{
	public class Game
	{

		public string Id { get; set; }
		public string Filename { get; set; }
		public bool Enabled { get; set; } = true;

		public string ReleaseId { get; set; }
		public bool Exists { get; set; }

		[JsonIgnoreAttribute]
		public Platform Platform { get; set; }

		public Game() { }

		public Game(PinballX.Models.Game xmlGame, string tablePath, Platform platform)
		{
			Platform = platform;
			updateFromGame(xmlGame, tablePath);
		}

		internal Game merge(PinballX.Models.Game xmlGame, string tablePath, Platform platform)
		{
			Platform = platform;
			updateFromGame(xmlGame, tablePath);
			return this;
		}

		private void updateFromGame(PinballX.Models.Game xmlGame, string tablePath)
		{
			Id = xmlGame.Description;
			Enabled = "true".Equals(xmlGame.Enabled, StringComparison.InvariantCultureIgnoreCase);

			if (File.Exists(tablePath + @"\" + xmlGame.Filename + ".vpt")) {
				Filename = xmlGame.Filename + ".vpt";
				Exists = true;
			} else if (File.Exists(tablePath + @"\" + xmlGame.Filename + ".vpx")) {
				Filename = xmlGame.Filename + ".vpx";
				Exists = true;
			} else {
				Filename = xmlGame.Filename;
				Exists = false;
			}
		}
	}
}
