using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
		// props
		public IReactiveDerivedList<DownloadItemViewModel> Jobs { get; }

		// output props
		private readonly ObservableAsPropertyHelper<bool> _isEmpty;
		public bool IsEmpty => _isEmpty.Value;

		public DownloadsViewModel(IJobManager jobManager)
		{
			Jobs = jobManager.CurrentJobs.CreateDerivedCollection(
				job => new DownloadItemViewModel(job),
				_ => true, 
				(x, y) => x.Job.CompareTo(y.Job),
				jobManager.WhenStatusChanged
			);

			jobManager.CurrentJobs.CountChanged
				.StartWith(0)
				.Select(_ => Jobs.Count == 0)
				.ToProperty(this, x => x.IsEmpty, out _isEmpty);
		}
	}
}
