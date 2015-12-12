using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Humanizer;
using ReactiveUI;
using Splat;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Download;

namespace VpdbAgent.ViewModels.Downloads
{
	public class DownloadItemViewModel : ReactiveObject
	{
		// status props
		public Job Job { get; }
		public bool Transferring { get { return _transferring; } set { this.RaiseAndSetIfChanged(ref _transferring, value); } }
		public bool Retryable { get { return _retryable; } set { this.RaiseAndSetIfChanged(ref _retryable, value); } }
		public string FileIcon { get { return _fileIcon; } set { this.RaiseAndSetIfChanged(ref _fileIcon, value); } }
		public int FileIconSize { get { return _fileIconSize; } set { this.RaiseAndSetIfChanged(ref _fileIconSize, value); } }
		public Brush StatusPanelForeground { get { return _statusPanelForeground; } set { this.RaiseAndSetIfChanged(ref _statusPanelForeground, value); } }
		public string StatusPanelIcon { get { return _statusPanelIcon; } set { this.RaiseAndSetIfChanged(ref _statusPanelIcon, value); } }
		public int StatusPanelIconSize { get { return _statusPanelIconSize; } set { this.RaiseAndSetIfChanged(ref _statusPanelIconSize, value); } }
		public double DownloadPercent { get { return _downloadPercent; } set { this.RaiseAndSetIfChanged(ref _downloadPercent, value); } }


		// commands
		public ReactiveCommand<object> CancelJob { get; protected set; } = ReactiveCommand.Create();
		public ReactiveCommand<object> DeleteJob { get; protected set; } = ReactiveCommand.Create();
		public ReactiveCommand<object> RetryJob { get; protected set; } = ReactiveCommand.Create();

		// label props
		public string DownloadSizeFormatted { get { return _downloadSizeFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadSizeFormatted, value); } }
		public string DownloadPercentFormatted { get { return _downloadPercentFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadPercentFormatted, value); } }
		public string DownloadSpeedFormatted { get { return _downloadSpeedFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadSpeedFormatted, value); } }
		public ObservableCollection<Inline> TitleLabel { get { return _titleLabel; } set { this.RaiseAndSetIfChanged(ref _titleLabel, value); } }
		public ObservableCollection<Inline> SubtitleLabel { get { return _subtitleLabel; } set { this.RaiseAndSetIfChanged(ref _subtitleLabel, value); } }
		public ObservableCollection<Inline> StatusPanelLabel { get { return _statusPanelLabel; } set { this.RaiseAndSetIfChanged(ref _statusPanelLabel, value); } }

		// privates
		private bool _transferring;
		private bool _retryable;
		private string _fileIcon;
		private int _fileIconSize;
		private Brush _statusPanelForeground;
		private string _statusPanelIcon;
		private int _statusPanelIconSize;
		private double _downloadPercent;
		private string _downloadSizeFormatted;
		private string _downloadPercentFormatted;
		private string _downloadSpeedFormatted;
		private ObservableCollection<Inline> _titleLabel;
		private ObservableCollection<Inline> _subtitleLabel;
		private ObservableCollection<Inline> _statusPanelLabel;

		// deps
		private static readonly IJobManager JobManager = Locator.CurrentMutable.GetService<IJobManager>();

		// brushes
		private static readonly Brush RedBrush = (Brush)System.Windows.Application.Current.FindResource("LightRedBrush");
		private static readonly Brush GreenBrush = (Brush)System.Windows.Application.Current.FindResource("LightGreenBrush");
		private static readonly Brush GreyBrush = (Brush)System.Windows.Application.Current.FindResource("LabelTextBrush");

		// icons
		private static readonly string WarningIcon = (string)System.Windows.Application.Current.FindResource("IconWarning");
		private static readonly string ClockIcon = (string)System.Windows.Application.Current.FindResource("IconClock");
		private static readonly string CheckIcon = (string)System.Windows.Application.Current.FindResource("IconCheck");
		private static readonly string CloseIcon = (string)System.Windows.Application.Current.FindResource("IconClose");
		private static readonly string VideoIcon = (string)System.Windows.Application.Current.FindResource("IconVideo");
		private static readonly string AudioIcon = (string)System.Windows.Application.Current.FindResource("IconAudio");
		private static readonly string RomIcon = (string)System.Windows.Application.Current.FindResource("IconRom");
		private static readonly string CameraIcon = (string)System.Windows.Application.Current.FindResource("IconCamera");
		private static readonly string DefaultFileIcon = (string)System.Windows.Application.Current.FindResource("IconFile");

		public DownloadItemViewModel(Job job)
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
						DownloadPercent = (double)progress.BytesReceived / Job.File.Bytes * 100;
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

						if (timespan.TotalMilliseconds > 0) {
							var downloadSpeed = 1000 * bytespan / timespan.TotalMilliseconds;
							DownloadSpeedFormatted = $"{downloadSpeed.Bytes().ToString("#.0")}/s";
						}

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
						DownloadSizeFormatted = (progress.TotalBytesToReceive > 0 ? progress.TotalBytesToReceive : Job.File.Bytes).Bytes().ToString("#.0");
					});
				});

			// abort job on command
			CancelJob.Subscribe(_ => { Job.Cancel(); });

			// retry job
			RetryJob.Subscribe(_ => { JobManager.RetryJob(Job); });
			
			// delete job
			DeleteJob.Subscribe(_ => { JobManager.DeleteJob(Job); });

			// setup icon
			SetupFileIcon();
		}

		private void OnStatusUpdated()
		{
			switch (Job.Status) {
				case Job.JobStatus.Aborted:
					StatusPanelForeground = RedBrush;
					StatusPanelIcon = CloseIcon;
					StatusPanelIconSize = 12;
					StatusPanelLabel = new ObservableCollection<Inline> { new Run("Cancelled"), };
					Retryable = true;
					OnFinished();
					break;

				case Job.JobStatus.Completed:
					StatusPanelForeground = GreenBrush;
					StatusPanelIcon = CheckIcon;
					StatusPanelIconSize = 16;
					StatusPanelLabel = new ObservableCollection<Inline> {
						new Run("Successfully downloaded "),
						new Run(Job.TransferredBytes.Bytes().ToString("#.0") + " ") {FontWeight = FontWeights.Bold},
						new Run(Job.FinishedAt.Humanize(false)),
						new Run(" at "),
						new Run(Job.DownloadBytesPerSecond.Bytes().ToString("#.0") + "/s") {FontWeight = FontWeights.Bold}
					};
					Retryable = false;
					OnFinished();
					break;

				case Job.JobStatus.Failed:
					StatusPanelForeground = RedBrush;
					StatusPanelIcon = WarningIcon;
					StatusPanelIconSize = 18;
					StatusPanelLabel = new ObservableCollection<Inline> {
						new Run("Error: "),
						new Run(Job.ErrorMessage)
					};
					Retryable = true;
					OnFinished();
					break;

				case Job.JobStatus.Queued:
					StatusPanelForeground = GreyBrush;
					StatusPanelIcon = ClockIcon;
					StatusPanelIconSize = 16;
					StatusPanelLabel = new ObservableCollection<Inline> { new Run("Transfer queued") };
					Retryable = false;
					Transferring = false;
					break;

				case Job.JobStatus.Transferring:
					StatusPanelForeground = Brushes.Transparent;
					Transferring = true;
					Retryable = false;
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

		private void SetupFileIcon()
		{
			switch (Job.FileType)
			{
				case FileType.TableMusic:
					TitleLabel = new ObservableCollection<Inline> {
						new Run(Job.Release.Game.DisplayName) {FontWeight = FontWeights.Bold},
						new Run(" – "),
						new Run(Job.Release.Name),
						new Run(Job.Version.Name),
					};
					SubtitleLabel = new ObservableCollection<Inline> { new Run(Job.File.Name) };
					FileIcon = AudioIcon;
					FileIconSize = 16;
					break;

				case FileType.WheelImage:
					TitleLabel = new ObservableCollection<Inline> { new Run(Job.Release.Game.DisplayName) { FontWeight = FontWeights.Bold } };
					SubtitleLabel = new ObservableCollection<Inline> { new Run("Wheel Image") };
					FileIcon = CameraIcon;
					FileIconSize = 16;
					break;

				case FileType.BackglassImage:
					TitleLabel = new ObservableCollection<Inline> { new Run(Job.Release.Game.DisplayName) { FontWeight = FontWeights.Bold } };
					SubtitleLabel = new ObservableCollection<Inline> { new Run("Backglass Image") };
					FileIcon = CameraIcon;
					FileIconSize = 16;
					break;

				case FileType.TableImage:
					TitleLabel = new ObservableCollection<Inline> { new Run(Job.Release.Game.DisplayName) { FontWeight = FontWeights.Bold } };
					SubtitleLabel = new ObservableCollection<Inline> { new Run("Table Image") };
					FileIcon = CameraIcon;
					FileIconSize = 16;
					break;

				case FileType.TableVideo:
					TitleLabel = new ObservableCollection<Inline> { new Run(Job.Release.Game.DisplayName) { FontWeight = FontWeights.Bold } };
					SubtitleLabel = new ObservableCollection<Inline> { new Run("Table Video") };
					FileIcon = VideoIcon;
					FileIconSize = 16;
					break;

				case FileType.Rom:
					TitleLabel = new ObservableCollection<Inline> { new Run(Job.Release.Game.DisplayName) { FontWeight = FontWeights.Bold } };
					SubtitleLabel = new ObservableCollection<Inline> {
						new Run("ROM: "),
						new Run(Job.File.Name) {FontWeight = FontWeights.Bold}
					};
					FileIcon = RomIcon;
					FileIconSize = 16;
					break;

				case FileType.TableFile:
				case FileType.TableScript:
				case FileType.TableAuxiliary:
				default:
					TitleLabel = new ObservableCollection<Inline> {
						new Run(Job.Release.Game.DisplayName) {FontWeight = FontWeights.Bold},
						new Run(" – "),
						new Run(Job.Release.Name)
					};
					SubtitleLabel = new ObservableCollection<Inline> { new Run(Job.File.Name) };
					FileIcon = DefaultFileIcon;
					FileIconSize = 16;
					break;
			}
		}
	}
}
