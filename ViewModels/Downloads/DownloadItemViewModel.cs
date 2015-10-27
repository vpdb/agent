using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Humanizer;
using ReactiveUI;
using VpdbAgent.Vpdb;

namespace VpdbAgent.ViewModels.Downloads
{
	public class DownloadItemViewModel : ReactiveObject
	{
		// status props
		public DownloadJob Job { get; }
		public bool Transferring { get { return _transferring; } set { this.RaiseAndSetIfChanged(ref _transferring, value); } }
		public Brush StatusPanelBackground { get { return _statusPanelBackground; } set { this.RaiseAndSetIfChanged(ref _statusPanelBackground, value); } }
		public string StatusPanelIcon { get { return _statusPanelIcon; } set { this.RaiseAndSetIfChanged(ref _statusPanelIcon, value); } }
		public double DownloadPercent { get { return _downloadPercent; } set { this.RaiseAndSetIfChanged(ref _downloadPercent, value); } }

		// label props
		public string DownloadSizeFormatted { get { return _downloadSizeFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadSizeFormatted, value); } }
		public string DownloadPercentFormatted { get { return _downloadPercentFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadPercentFormatted, value); } }
		public string DownloadSpeedFormatted { get { return _downloadSpeedFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadSpeedFormatted, value); } }
		public ObservableCollection<Inline> StatusPanelLabel { get { return _statusPanelLabel; } set { this.RaiseAndSetIfChanged(ref _statusPanelLabel, value); } }

		// privates
		private bool _transferring;
		private Brush _statusPanelBackground;
		private string _statusPanelIcon;
		private double _downloadPercent;
		private string _downloadSizeFormatted;
		private string _downloadPercentFormatted;
		private string _downloadSpeedFormatted;
		private ObservableCollection<Inline> _statusPanelLabel;

		// brushes
		private static readonly Brush RedBrush = (Brush)System.Windows.Application.Current.FindResource("DarkRedBrush");
		private static readonly Brush GreenBrush = (Brush)System.Windows.Application.Current.FindResource("DarkGreenBrush");
		private static readonly Brush OrangeBrush = (Brush)System.Windows.Application.Current.FindResource("DarkOrangeBrush");

		// icons
		private static readonly string WarningIcon = (string)System.Windows.Application.Current.FindResource("IconWarning");
		private static readonly string ClockIcon = (string)System.Windows.Application.Current.FindResource("IconClock");
		private static readonly string CheckIcon = (string)System.Windows.Application.Current.FindResource("IconCheckCircle");
		private static readonly string CloseIcon = (string)System.Windows.Application.Current.FindResource("IconCloseCircle");

		public DownloadItemViewModel(DownloadJob job)
		{
			Job = job;
			Job.WhenStatusChanges.Subscribe(status => { OnStatusUpdated(); });
			OnStatusUpdated();

			// update progress every 300ms
			Job.WhenDownloadProgresses
				.Sample(TimeSpan.FromMilliseconds(300))
				.Where(x => !job.IsFinished)
				.Subscribe(progress => {
					// on main thread
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						DownloadPercent = (double)progress.BytesReceived / Job.File.Reference.Bytes * 100;
						DownloadPercentFormatted = $"{Math.Round(DownloadPercent)}%";
					});
				});

			// update download speed every 1.5 seconds
			var lastUpdatedProgress = DateTime.Now;
			long bytesReceived = 0;
			Job.WhenDownloadProgresses
				.Sample(TimeSpan.FromMilliseconds(1500))
				.Where(x => !job.IsFinished)
				.Subscribe(progress => {
					// on main thread
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						var timespan = DateTime.Now - lastUpdatedProgress;
						var bytespan = progress.BytesReceived - bytesReceived;
						var downloadSpeed = bytespan / timespan.Seconds;

						DownloadSpeedFormatted = $"{downloadSpeed.Bytes().ToString("#.0")}/s";

						bytesReceived = progress.BytesReceived;
						lastUpdatedProgress = DateTime.Now;
					});
				});

			// update initial size only once
			Job.WhenDownloadProgresses
				.Take(1)
				.Subscribe(progress => {
					// on main thread
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						DownloadSizeFormatted = (progress.TotalBytesToReceive > 0 ? progress.TotalBytesToReceive : Job.File.Reference.Bytes).Bytes().ToString("#.0");
					});
				});
		}

		private void OnStatusUpdated()
		{
			switch (Job.Status) {
				case DownloadJob.JobStatus.Aborted:
					StatusPanelBackground = RedBrush;
					StatusPanelIcon = CloseIcon;
					OnFinished();
					break;

				case DownloadJob.JobStatus.Completed:
					StatusPanelBackground = GreenBrush;
					StatusPanelIcon = CheckIcon;
					OnFinished();
					StatusPanelLabel = new ObservableCollection<Inline>
					{
						new Run("Successfully downloaded "),
						new Run(Job.TransferredBytes.Bytes().ToString("#.0") + " ") {FontWeight = FontWeights.Bold},
						new Run(Job.FinishedAt.Humanize()),
						new Run(" at "),
						new Run(Job.DownloadBytesPerSecond.Bytes().ToString("#.0") + "/s") {FontWeight = FontWeights.Bold}
					};
					break;

				case DownloadJob.JobStatus.Failed:
					StatusPanelBackground = RedBrush;
					StatusPanelIcon = WarningIcon;
					OnFinished();
					StatusPanelLabel = new ObservableCollection<Inline>
					{
						new Run("Error: "),
						new Run(Job.ErrorMessage)
					};
					break;

				case DownloadJob.JobStatus.Queued:
					StatusPanelBackground = OrangeBrush;
					StatusPanelIcon = ClockIcon;
					Transferring = false;
					StatusPanelLabel = new ObservableCollection<Inline>
					{
						new Run("Transfer queued")
					};
					break;

				case DownloadJob.JobStatus.Transferring:
					StatusPanelBackground = Brushes.Transparent;
					Transferring = true;
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void OnFinished()
		{
			Transferring = false;
			DownloadPercent = 0;
			DownloadPercentFormatted = null;
			DownloadSpeedFormatted = null;
			DownloadSizeFormatted = null;
			Job.RaisePropertyChanged();
		}

	}
}
