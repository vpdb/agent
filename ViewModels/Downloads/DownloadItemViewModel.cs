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

		// props
		public Brush StatusPanelBackground { get { return _statusPanelBackground; } set { this.RaiseAndSetIfChanged(ref _statusPanelBackground, value); } }
		public bool StatusPanelVisible { get { return _statusPanelVisible; } set { this.RaiseAndSetIfChanged(ref _statusPanelVisible, value); } }

		// privates
		private Brush _statusPanelBackground = Brushes.Transparent;
		private bool _statusPanelVisible;

		// static stuff
		private static readonly Brush RedBrush = (Brush)System.Windows.Application.Current.FindResource("DarkRedBrush");
		private static readonly Brush GreenBrush = (Brush)System.Windows.Application.Current.FindResource("DarkGreenBrush");
		private static readonly Brush OrangeBrush = (Brush)System.Windows.Application.Current.FindResource("DarkOrangeBrush");

		public DownloadItemViewModel(DownloadJob job)
		{
			Job = job;
			Job.WhenStatusChanges.Subscribe(status => { UpdateStatusPanel(); });
			UpdateStatusPanel();
		}

		private void UpdateStatusPanel()
		{
			switch (Job.Status) {
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
		}
	}
}
