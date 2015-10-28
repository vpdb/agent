using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Vpdb;

namespace VpdbAgent.ViewModels.Downloads
{
	public class DownloadsViewModel : ReactiveObject
	{
		// deps
		private static readonly IDownloadManager DownloadManager = Locator.CurrentMutable.GetService<IDownloadManager>();

		// props
		public IReactiveDerivedList<DownloadItemViewModel> Jobs { get; }

		public DownloadsViewModel()
		{
			Jobs = DownloadManager.CurrentJobs.CreateDerivedCollection(
				job => new DownloadItemViewModel(job),
				x => true, 
				(x, y) => x.Job.CompareTo(y.Job),
				DownloadManager.WhenStatusChanged
			);
		}
	}
}
