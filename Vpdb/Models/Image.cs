using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class Image : ReactiveObject
	{
		private string _url;

		[DataMember] public string Url { get { return _url; } set { this.RaiseAndSetIfChanged(ref _url, value); } }
		[DataMember] public bool IsProtected { get; set; }
		[DataMember] public int Width { get; set; }
		[DataMember] public int Height { get; set; }
	}
}
