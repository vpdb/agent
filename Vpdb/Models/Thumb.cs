using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class Thumb : ReactiveObject
	{
		private Image _image;

		[DataMember] public Image Image { get { return _image; } set { this.RaiseAndSetIfChanged(ref _image, value); } }
		[DataMember] public Flavor Flavor { get; set; }
	}
}
