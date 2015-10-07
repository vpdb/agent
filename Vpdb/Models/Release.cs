using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Models
{
	public class Release
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public DateTime CreatedAt { get; set; }
		public List<Author> Authors { get; set; }
		public ReleaseCounter Counter { get; set; }
		public Game Game { get; set; }
		public Version LatestVersion { get; set; }
		public bool Starred { get; set; }

		public class ReleaseCounter
		{
			public int Comments { get; set; }
			public int Stars { get; set; }
			public int Downloads { get; set; }
		}
	}
}