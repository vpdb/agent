using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbThumb : ReactiveObject
	{
		private VpdbImage _image;

		[DataMember] public VpdbImage Image { get { return _image; } set { this.RaiseAndSetIfChanged(ref _image, value); } }
		[DataMember] public VpdbFlavor Flavor { get; set; }
	}
}
