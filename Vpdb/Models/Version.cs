using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace VpdbAgent.Vpdb.Models
{
	public class Version : ReactiveObject
	{
		private VersionThumb _versionThumb;

		[JsonProperty(PropertyName = "version")]
		[DataMember] public string Name { get; set; }
		[DataMember] public VersionThumb Thumb { get { return _versionThumb; } set { this.RaiseAndSetIfChanged(ref _versionThumb, value); } }
		[DataMember] public List<File> Files { get; set; }

		public override string ToString()
		{
			return $"{Name} ({Files.Count} files)";
		}

		public class VersionThumb : ReactiveObject
		{
			private Image _image;

			[DataMember] public Image Image { get { return _image; } set { this.RaiseAndSetIfChanged(ref _image, value); } }
			[DataMember] public Flavor Flavor { get; set; }
		}
	}
}
