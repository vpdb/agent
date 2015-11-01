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
		[JsonConverter(typeof(StringEnumConverter))] public ELighting Lighting { get; set; }
		[JsonConverter(typeof(StringEnumConverter))] public EOrientation Orientation { get; set; }

		public override string ToString()
		{
			return $"{Lighting}/{Orientation}";
		}

		public enum ELighting { Day, Night, Any }
		public enum EOrientation { FS, WS, Any }
	}
}
