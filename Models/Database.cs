using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Models
{
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
