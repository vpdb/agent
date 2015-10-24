using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VpdbAgent.Vpdb;

namespace VpdbAgent.ViewModels.Downloads
{
	public class DownloadItemViewModel : ReactiveObject
	{
		public DownloadJob Job { get; }

		public DownloadItemViewModel(DownloadJob job)
		{
			Job = job;
		}
	}
}
