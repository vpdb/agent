using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Network
{
	public class SnakeCasePropertyNamesContractResolver : DefaultContractResolver
	{
		protected override string ResolvePropertyName(string propertyName)
		{
			return Regex.Replace(propertyName, "([a-z])([A-Z])", "$1_$2").ToLower();
		}
	}
}
