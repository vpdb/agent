using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class TableFile : ReactiveObject
	{
		private Image _thumb;

		[DataMember] public DateTime ReleasedAt { get; set; }
		[DataMember] public Flavor Flavor { get; set; }
		[DataMember] public Dictionary<string, FileReference> Media { get; set; }
		[DataMember] public List<FileCompatibility> Compatibility;
		[DataMember] [JsonProperty(PropertyName = "file")] public FileReference Reference { get; set; }
		[DataMember] public Image Thumb { get { return _thumb; } set { this.RaiseAndSetIfChanged(ref _thumb, value); } }

		public override string ToString()
		{
			return $"{Flavor.Lighting}/{Flavor.Orientation} ({string.Join(",", Compatibility.Select(c => c.Label))})";
		}

		public class FileCompatibility
		{
			public string Id { get; set; }
			public string Label { get; set; }
			public Platform Platform { get; set; }
			public string MajorVersion { get; set; }
			public string DownloadUrl { get; set; }
			public DateTime BuiltAt { get; set; }
			public bool IsRange { get; set; }

			public override string ToString()
			{
				return $"{Label} ({Platform})";
			}
		}

		/// <summary>
		/// Platform as defined in the file's build at VPDB.
		/// </summary>
		public enum Platform
		{
			VP, FP
		}
	}
}
