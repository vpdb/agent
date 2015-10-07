using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using System.Runtime.Serialization;

namespace VpdbAgent.Vpdb.Models
{
	public class Version : ReactiveObject
	{
		private VersionThumb versionThumb;

		[JsonProperty(PropertyName = "version")]
		public string Name { get; set; }
		[DataMember]
		public VersionThumb Thumb
		{
			get { return versionThumb; }
			set { this.RaiseAndSetIfChanged(ref versionThumb, value); }

		}
		public List<File> Files { get; set; }

		public class VersionThumb : ReactiveObject
		{
			private Image image;

			[DataMember]
			public Image Image
			{
				get { return image; }
				set { this.RaiseAndSetIfChanged(ref image, value); }
			}
			public Flavor Flavor { get; set; }
		}
	}
}
