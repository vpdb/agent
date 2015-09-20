using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Models
{
	public class Platform
	{
		public string Path { get; set; }
		public string Name { get; set; }
		public bool Enabled { get; set; } = true;
		public string WorkingPath { get; set; }
		public string TablePath { get; set; }
		public string Executable { get; set; }
		public string Parameters { get; set; }
		public Type Type { get; set; }
		public List<Game> Games { get; set; } = new List<Game>();

		public Platform AddGame(Game game)
		{
			Games.Add(game);
			return this;
		}
	}

	public enum Type
	{
		VP, FP, CUSTOM
	}
}
