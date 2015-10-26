using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Humanizer;
using Humanizer.Localisation;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Vpdb.Models;
using File = VpdbAgent.Vpdb.Models.File;
using Version = VpdbAgent.Vpdb.Models.Version;

namespace VpdbAgent.Vpdb
{
	public class DownloadJob : ReactiveObject
	{
		// props	
		public Release Release { get; }
		public Version Version { get; }
		public File File { get; }
		public string Filename { get; }
		public JobStatus Status { get; private set; }

		public string DownloadSizeFormatted { get { return _downloadSizeFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadSizeFormatted, value); } }
		public string DownloadPercentFormatted { get { return _downloadPercentFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadPercentFormatted, value); } }
		public string DownloadSpeedFormatted { get { return _downloadSpeedFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadSpeedFormatted, value); } }
		public double DownloadPercent { get { return _downloadPercent; } set { this.RaiseAndSetIfChanged(ref _downloadPercent, value); } }

		private static readonly Brush GreenBrush = (Brush)System.Windows.Application.Current.FindResource("DarkGreenBrush");
		private static readonly Brush RedBrush = (Brush)System.Windows.Application.Current.FindResource("DarkRedBrush");

		public readonly Uri Uri;
		public readonly WebClient Client;

		public IObservable<JobStatus> WhenStatusChanges => _status;
		public IObservable<DownloadProgressChangedEventArgs> WhenDownloadProgresses => _progress;

		private string _downloadSizeFormatted;
		private string _downloadPercentFormatted;
		private string _downloadSpeedFormatted;
		private double _downloadPercent;
		private readonly Subject<JobStatus> _status = new Subject<JobStatus>();
		private readonly Subject<DownloadProgressChangedEventArgs> _progress = new Subject<DownloadProgressChangedEventArgs>();

		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();

		public DownloadJob()
		{
			_status.Subscribe(status => {
				Status = status;
			});
		}

		public DownloadJob(Release release, File file, IVpdbClient client) : this()
		{
			Uri = client.GetUri(file.Reference.Url);
			Client = client.GetWebClient();
			File = file;
			Filename = file.Reference.Name;
			Release = release;
			Version = release.Versions.FirstOrDefault(version => version.Files.Contains(file));

			Logger.Info("Creating new download job for {0}.", Uri.AbsoluteUri);
		}

		public void OnStart()
		{
			_status.OnNext(JobStatus.Transferring);
			Client.DownloadProgressChanged += OnProgressChanged;

			// update progress every 300ms
			WhenDownloadProgresses
				.Sample(TimeSpan.FromMilliseconds(300))
				.Subscribe(progress => {
					// on main thread
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						DownloadPercent = (double)progress.BytesReceived / File.Reference.Bytes * 100;
						DownloadPercentFormatted = $"{Math.Round(DownloadPercent)}%";
					});
				});

			// update download speed every 1.5 seconds
			var lastUpdatedProgress = DateTime.Now;
			long bytesReceived = 0;
			WhenDownloadProgresses
				.Sample(TimeSpan.FromMilliseconds(1500))
				.Subscribe(progress => {
					// on main thread
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						var timespan = DateTime.Now - lastUpdatedProgress;
						var bytespan = progress.BytesReceived - bytesReceived;
						var downloadSpeed = bytespan / timespan.Seconds;

						DownloadSpeedFormatted = $"{downloadSpeed.Bytes().ToString("#.#")}/s";

						bytesReceived = progress.BytesReceived;
						lastUpdatedProgress = DateTime.Now;
					});
				});

			// update initial size only once
			WhenDownloadProgresses
				.Take(1)
				.Subscribe(progress => {
					// on main thread
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						DownloadSizeFormatted = (progress.TotalBytesToReceive > 0 ? progress.TotalBytesToReceive : File.Reference.Bytes).Bytes().ToString("#.#");
					});
				});
		}

		public void OnSuccess()
		{
			// on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				_status.OnNext(JobStatus.Completed);
			});
			Client.DownloadProgressChanged -= OnProgressChanged;
		}

		public void OnFailure(Exception e)
		{
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				_status.OnNext(JobStatus.Failed);
			});
			Client.DownloadProgressChanged -= OnProgressChanged;
		}

		private void OnProgressChanged(object sender, DownloadProgressChangedEventArgs p)
		{
			_progress.OnNext(p);
		}

		/// <summary>
		/// Statuses of a download job
		/// </summary>
		public enum JobStatus
		{
			/// <summary>
			/// Job is about to be transferred but still waiting for a slot to be freed
			/// </summary>
			Queued, 
			
			/// <summary>
			/// Job is currently transferring
			/// </summary>
			Transferring, 
			
			/// <summary>
			/// Job has successfully terminated
			/// </summary>
			Completed, 
			
			/// <summary>
			/// Job terminated but failed
			/// </summary>
			Failed, 
			
			/// <summary>
			/// Job was aborted
			/// </summary>
			Aborted
		}
	}
}
