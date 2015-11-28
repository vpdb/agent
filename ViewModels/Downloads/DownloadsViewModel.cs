using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
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

		// output props
		private readonly ObservableAsPropertyHelper<bool> _isEmpty;
		public bool IsEmpty => _isEmpty.Value;

		public DownloadsViewModel()
		{
			Jobs = JobManager.CurrentJobs.CreateDerivedCollection(
				job => new DownloadItemViewModel(job),
				x => true, 
				(x, y) => x.Job.CompareTo(y.Job),
				JobManager.WhenStatusChanged
			);

			Jobs.CountChanged
				.Select(_ => Jobs.Count == 0)
				.StartWith(Jobs.Count == 0)
				.ToProperty(this, x => x.IsEmpty, out _isEmpty);
		}
	}
}
