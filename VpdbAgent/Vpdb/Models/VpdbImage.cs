using System.Runtime.Serialization;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbImage : ReactiveObject
	{
		private string _url;

		public string Url { get { return _url; } set { this.RaiseAndSetIfChanged(ref _url, value); } }
		public bool IsProtected { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
	}
}
