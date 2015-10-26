using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ReactiveUI;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.ViewModels.Downloads
{
	public class DownloadItemViewModel : ReactiveObject
	{
		public DownloadJob Job { get; }
		public Brush StatusPanelBackground { get; private set; } = Brushes.Transparent;
		public bool StatusPanelVisible { get; private set; } = false;

		private static readonly Brush RedBrush = (Brush)System.Windows.Application.Current.FindResource("DarkRedBrush");
		private static readonly Brush GreenBrush = (Brush)System.Windows.Application.Current.FindResource("DarkGreenBrush");
		private static readonly Brush OrangeBrush = (Brush)System.Windows.Application.Current.FindResource("DarkOrangeBrush");

		public DownloadItemViewModel(DownloadJob job)
		{
			Job = job;

			Job.WhenStatusChanges.Subscribe(status =>
			{
				switch (status) {
					case DownloadJob.JobStatus.Aborted:
						StatusPanelBackground = RedBrush;
						StatusPanelVisible = true;
						break;
					case DownloadJob.JobStatus.Completed:
						StatusPanelBackground = GreenBrush;
						StatusPanelVisible = true;
						break;
					case DownloadJob.JobStatus.Failed:
						StatusPanelBackground = RedBrush;
						StatusPanelVisible = true;
						break;
					case DownloadJob.JobStatus.Queued:
						StatusPanelBackground = OrangeBrush;
						StatusPanelVisible = true;
						break;
					default:
						StatusPanelBackground = Brushes.Transparent;
						StatusPanelVisible = false;
						break;
				}
			});
		}
	}
}
