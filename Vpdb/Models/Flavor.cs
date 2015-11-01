using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VpdbAgent.Vpdb.Models
{
	public class Flavor
	{
		[JsonConverter(typeof(StringEnumConverter))] public LightingValue Lighting { get; set; }
		[JsonConverter(typeof(StringEnumConverter))] public OrientationValue Orientation { get; set; }

		public override string ToString()
		{
			return $"{Lighting}/{Orientation}";
		}

		public enum LightingValue { Day, Night, Any }
		public enum OrientationValue { FS, WS, Any }
	}
}
