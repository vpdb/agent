using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbTableFile : ReactiveObject
	{
		private VpdbImage _thumb;

		[DataMember] public DateTime ReleasedAt { get; set; }
		[DataMember] public VpdbFlavor Flavor { get; set; }
		[DataMember] [BsonRef("files")] public VpdbFile PlayfieldImage { get; set; }
		[DataMember] [BsonRef("files")] public VpdbFile PlayfieldVideo { get; set; }
		//[DataMember] public Dictionary<string, VpdbFile> Media { get; set; }
		[DataMember] public List<VpdbCompatibility> Compatibility;
		[DataMember] [BsonRef("files")] [JsonProperty(PropertyName = "file")] public VpdbFile Reference { get; set; }
		[DataMember] public VpdbImage Thumb { get { return _thumb; } set { this.RaiseAndSetIfChanged(ref _thumb, value); } }

		public override string ToString()
		{
			return $"{Flavor.Lighting}/{Flavor.Orientation} ({string.Join(",", Compatibility.Select(c => c.Label))})";
		}

		public class VpdbCompatibility
		{
			public string Id { get; set; }
			public string Label { get; set; }
			public VpdbPlatform Platform { get; set; }
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
		public enum VpdbPlatform
		{
			/// <summary>
			/// Visual Pinball
			/// </summary>
			VP,

			/// <summary>
			/// Future Pinball
			/// </summary>
			FP,

			/// <summary>
			/// For testing only
			/// </summary>
			[Obsolete("Only use for testing!")]
			Unknown 
		}
	}
}
