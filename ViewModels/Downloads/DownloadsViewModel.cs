using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Download;

namespace VpdbAgent.ViewModels.Downloads
{
	public class DownloadsViewModel : ReactiveObject
	{
		// deps
		private static readonly IJobManager JobManager = Locator.CurrentMutable.GetService<IJobManager>();

		// props
		public IReactiveDerivedList<DownloadItemViewModel> Jobs { get; }

		public DownloadsViewModel()
		{
			Jobs = JobManager.CurrentJobs.CreateDerivedCollection(
				job => new DownloadItemViewModel(job),
				x => true, 
				(x, y) => x.Job.CompareTo(y.Job),
				JobManager.WhenStatusChanged
			);
		}
	}
}
