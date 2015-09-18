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
		public string Name { get; set; }
		public VersionThumb Thumb { get; set; }
		public List<File> Files { get; set; }

		public class VersionThumb
		{
			public Image Image { get; set; }
			public Flavor Flavor { get; set; }
		}
	}
}
