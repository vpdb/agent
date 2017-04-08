using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbFlavor
	{
		[DataMember] [JsonConverter(typeof(StringEnumConverter))] public VpdbLighting Lighting { get; set; }
		[DataMember] [JsonConverter(typeof(StringEnumConverter))] public VpdbOrientation Orientation { get; set; }

		public override string ToString()
		{
			return $"{Lighting}/{Orientation}";
		}

		public enum VpdbLighting { Day, Night, Any }
		public enum VpdbOrientation { FS, WS, Any }
	}
}
