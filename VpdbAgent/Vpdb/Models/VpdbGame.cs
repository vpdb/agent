using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using Newtonsoft.Json;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbGame
	{
		public string Id { get; set; }
		public string Title { get; set; }
		public string Manufacturer { get; set; }
		public int Year { get; set; }
		[BsonRef("files")] public VpdbFile Backglass { get; set; }
		[BsonRef("files")] public VpdbFile Logo { get; set; }

		[JsonIgnore] [BsonIgnore]
		public string DisplayName => Manufacturer != null ? $"{Title} ({Manufacturer} {Year})" : Title;
	}
}
