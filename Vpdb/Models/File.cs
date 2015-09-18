using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Models
{
	public class File
	{
		public DateTime ReleasedAt { get; set; }
		public Flavor Flavor { get; set; }
		public List<string> Compatibility;
		[JsonProperty(PropertyName = "file")]
		public FileReference Reference { get; set; }

		public class FileReference
		{
			public string Id { get; set; }
			public string Name { get; set; }
			public long Bytes { get; set; }
			public string MimeType { get; set; }
		}
	}
}
