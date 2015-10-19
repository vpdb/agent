using System.Collections.Generic;
using System.Linq;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Models
{
	/// <summary>
	/// The data structure that is saved as a .json file for each platform.
	/// </summary>
	public class PlatformDatabase
	{
		public IEnumerable<Game> Games { set; get; } = new List<Game>();

		public PlatformDatabase() { }

		public PlatformDatabase(IEnumerable<Game> games)
		{
			Games = games;
		}

		public override string ToString()
		{
			return $"[PlatformDB] {Games.Count()} game(s)";
		}
	}
}
