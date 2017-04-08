using System.Runtime.Serialization;
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
