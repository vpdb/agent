using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using LiteDB;
using ReactiveUI;
using VpdbAgent.Application;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbTableFile : ReactiveObject
	{
		private VpdbImage _thumb;

		[DataMember] public DateTime ReleasedAt { get; set; }
		[DataMember] public VpdbFlavor Flavor { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableFiles)] public VpdbFile PlayfieldImage { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableFiles)] public VpdbFile PlayfieldVideo { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableFiles)] [JsonProperty(PropertyName = "file")] public VpdbFile Reference { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableBuilds)] public List<VpdbCompatibility> Compatibility { get; set; }
		[DataMember] public VpdbImage Thumb { get { return _thumb; } set { this.RaiseAndSetIfChanged(ref _thumb, value); } }
		[DataMember] public TableFileCounter Counter { get; set; }

		public override string ToString()
		{
			return $"{Flavor.Lighting}/{Flavor.Orientation} ({string.Join(",", Compatibility.Select(c => c.Label))})";
		}

		public class VpdbCompatibility
		{
			[DataMember] [BsonId] public string Id { get; set; }
			[DataMember] public string Label { get; set; }
			[DataMember] public VpdbPlatform Platform { get; set; }
			[DataMember] public string MajorVersion { get; set; }
			[DataMember] public string DownloadUrl { get; set; }
			[DataMember] public DateTime BuiltAt { get; set; }
			[DataMember] public bool IsRange { get; set; }

			public override string ToString()
			{
				return $"{Label} ({Platform})";
			}
		}

		public class TableFileCounter
		{
			[DataMember] public int Downloads { get; set; }
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
