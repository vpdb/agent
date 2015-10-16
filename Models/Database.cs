using System.Collections.Generic;

namespace VpdbAgent.Models
{

	/// <summary>
	/// The data structure that is saved as a .json file for each platform.
	/// </summary>
	public class Database
	{
		public IEnumerable<Game> Games { set; get; } = new List<Game>();

		public Database() { }

		public Database(IEnumerable<Game> games)
		{
			Games = games;
		}
	}
}
