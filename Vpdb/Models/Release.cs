using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Models
{
	public class Release
	{
		public string Id;
		public string Name;
		public DateTime CreatedAt;
		public List<Author> Authors;
		public ReleaseCounter Counter;
		public Game Game;
		public Version LatestVersion;

		public class ReleaseCounter
		{
			public int Comments;
			public int Stars;
			public int Downloads;
		}
	}
}