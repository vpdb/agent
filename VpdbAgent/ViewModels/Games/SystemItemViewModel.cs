using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.ViewModels.Games
{
	public class SystemItemViewModel : ReactiveObject
	{
		public PinballXSystem System { get; }
		public bool IsExpanded { get; set; } = true;

		public SystemItemViewModel(PinballXSystem system)
		{
			System = system;
		}
	}
}
