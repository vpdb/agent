using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VpdbAgent.Vpdb.Models
{
	public class Game
	{
		public string Id { get; set; }
		public string Title { get; set; }
		public string Manufacturer { get; set; }
		public int Year { get; set; }

		[JsonIgnore]
		public string DisplayName => Manufacturer != null ? $"{Title} ({Manufacturer} {Year})" : Title;
	}
}
