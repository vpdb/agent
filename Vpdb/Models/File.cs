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
		public DateTime ReleasedAt;
		public Flavor Flavor;
		public List<string> Compatibility;
		[JsonProperty(PropertyName = "file")]
		public FileReference Reference;

		public class FileReference
		{
			public string Id;
			public string Name;
			public long Bytes;
			public string MimeType;
		}
	}
}
