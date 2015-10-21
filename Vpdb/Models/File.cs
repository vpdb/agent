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
		public Dictionary<string, FileReference> Media { get; set; }
		public List<FileCompatibility> Compatibility;
		[JsonProperty(PropertyName = "file")]
		public FileReference Reference { get; set; }

		public override string ToString()
		{
			return $"{Flavor.Lighting}/{Flavor.Orientation} ({string.Join(",", Compatibility.Select(c => c.Label))})";
		}

		public class FileCompatibility
		{
			public string Id { get; set; }
			public string Label { get; set; }
			public string Platform { get; set; }
			public string MajorVersion { get; set; }
			public string DownloadUrl { get; set; }
			public DateTime BuiltAt { get; set; }
			public bool IsRange { get; set; }

			public override string ToString()
			{
				return Label;
			}
		}
	}
}
