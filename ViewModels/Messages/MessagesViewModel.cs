using ReactiveUI;
using Splat;
using VpdbAgent.ViewModels.Downloads;
using VpdbAgent.Vpdb.Download;

namespace VpdbAgent.ViewModels.Messages
{
	public class MessagesViewModel : ReactiveObject
	{
		// deps
		private static readonly IJobManager JobManager = Locator.CurrentMutable.GetService<IJobManager>();

		// props
		public IReactiveDerivedList<DownloadItemViewModel> Jobs { get; }

		public MessagesViewModel()
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
