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
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();
		private static readonly IVpdbClient VpdbClient = Locator.CurrentMutable.GetService<IVpdbClient>();
		private static readonly IDownloadManager DownloadManager = Locator.CurrentMutable.GetService<IDownloadManager>();

		public IReactiveDerivedList<DownloadItemViewModel> Jobs { get; }

		public DownloadsViewModel()
		{
			Jobs = DownloadManager.CurrentJobs.CreateDerivedCollection(job => new DownloadItemViewModel(job));
		}
	}
}
