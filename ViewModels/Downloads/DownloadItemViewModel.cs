using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ReactiveUI;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.ViewModels.Downloads
{
	public class DownloadItemViewModel : ReactiveObject
	{
		public DownloadJob Job { get; }

		public DownloadItemViewModel()
		{
			var dep = new DependencyObject();
			if (!DesignerProperties.GetIsInDesignMode(dep)) {
				throw new InvalidOperationException("I'm only accessible in design mode.");
			}
			Job = new DownloadJob(new Release(), new File(), null);
		}

		public DownloadItemViewModel(DownloadJob job)
		{
			Job = job;
		}
	}
}
