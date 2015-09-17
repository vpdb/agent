using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Models
{
	public class Version
	{
		[JsonProperty(PropertyName = "version")]
		public string Name;
		public VersionThumb Thumb;
		public List<File> Files;

		public class VersionThumb
		{
			public Image Image;
			public Flavor Flavor;
		}
	}
}
