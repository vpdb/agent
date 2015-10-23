using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace VpdbAgent.ViewModels
{
	public class MainViewModel : ReactiveObject, IRoutableViewModel
	{
		// screen
		public IScreen HostScreen { get; protected set; }
		public string UrlPathSegment => "main";

		public MainViewModel(IScreen screen)
		{
			HostScreen = screen;
		}
	}
}
