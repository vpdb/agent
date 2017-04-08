using ReactiveUI;
using System.Runtime.Serialization;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbImage : ReactiveObject
	{
		private string _url;

		[DataMember] public string Url { get { return _url; } set { this.RaiseAndSetIfChanged(ref _url, value); } }
		[DataMember] public bool IsProtected { get; set; }
		[DataMember] public int Width { get; set; }
		[DataMember] public int Height { get; set; }
	}
}
