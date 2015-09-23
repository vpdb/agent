using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Models
{
	public class Platform
	{
		public string Name { get; set; }
		public bool Enabled { get; set; } = true;
		public string WorkingPath { get; set; }
		public string TablePath { get; set; }
		public string Executable { get; set; }
		public string Parameters { get; set; }
		public PlatformType Type { get; set; }
		public string DatabasePath { get; set; }
		public string MediaPath { get; set; }
		public String DatabaseFile { get { return DatabasePath + @"\vpdb.json"; } }


		public Platform()
		{
		}

		public Platform(PinballXSystem system)
		{
			Name = system.Name;
			Enabled = system.Enabled;
			WorkingPath = system.WorkingPath;
			TablePath = system.TablePath;
			Executable = system.Executable;
			Parameters = system.Parameters;
			Type = system.Type;
			DatabasePath = system.DatabasePath;
			MediaPath = system.MediaPath;
		}

//		public Platform AddGame(Game game)
//		{
//			Games.Add(game);
//			return this;
//		}

		public enum PlatformType
		{
			VP, FP, CUSTOM
		}
	}
}
