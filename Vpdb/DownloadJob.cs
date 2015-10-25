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
		public readonly Uri Uri;
		public readonly string Filename;
		public readonly WebClient Client;
		public readonly Release Release;
		public readonly File File;
		public readonly Version Version;
		public string DownloadSizeFormatted { get { return _downloadSizeFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadSizeFormatted, value); } }
		public string DownloadPercentFormatted { get { return _downloadPercentFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadPercentFormatted, value); } }
		public string DownloadSpeedFormatted { get { return _downloadSpeedFormatted; } set { this.RaiseAndSetIfChanged(ref _downloadSpeedFormatted, value); } }
		public double DownloadPercent { get { return _downloadPercent; } set { this.RaiseAndSetIfChanged(ref _downloadPercent, value); } }

		public IObservable<DownloadProgressChangedEventArgs> WhenDownloadProgresses => _progress;

		private string _downloadSizeFormatted;
		private string _downloadPercentFormatted;
		private string _downloadSpeedFormatted;
		private double _downloadPercent;
		private readonly Subject<DownloadProgressChangedEventArgs> _progress = new Subject<DownloadProgressChangedEventArgs>();

		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();

		public DownloadJob(Release release, File file, IVpdbClient client)
		{
			
			Uri = client.GetUri(file.Reference.Url);
			Client = client.GetWebClient();
			File = file;
			Filename = file.Reference.Name;
			Release = release;
			Version = release.Versions.FirstOrDefault(version => version.Files.Contains(file));

			Logger.Info("Creating new download job for {0}.", Uri.AbsoluteUri);

			Client.DownloadProgressChanged += ProgressChanged;

			// update progress every 300ms
			WhenDownloadProgresses
				.Sample(TimeSpan.FromMilliseconds(300))
				.Subscribe(progress => {
					// on main thread
					Application.Current.Dispatcher.Invoke(delegate
					{
						DownloadPercent = progress.ProgressPercentage;
						DownloadPercentFormatted = $"{Math.Round(DownloadPercent)}%";
					});
				});

			// update download speed every 1.5 second
			var lastUpdatedProgress = DateTime.Now;
			long bytesReceived = 0;
			WhenDownloadProgresses
				.Sample(TimeSpan.FromMilliseconds(1500))
				.Subscribe(progress => {
					// on main thread
					Application.Current.Dispatcher.Invoke(delegate
					{
						var timespan = DateTime.Now - lastUpdatedProgress;
						var bytespan = progress.BytesReceived - bytesReceived;
						var downloadSpeed = bytespan / timespan.Seconds;

						DownloadSpeedFormatted = $"{BytesToString(downloadSpeed)}/s";

						bytesReceived = progress.BytesReceived;
						lastUpdatedProgress = DateTime.Now;
					});
				});

			// update initial size only once
			WhenDownloadProgresses
				.Take(1)
				.Subscribe(progress => {
					// on main thread
					Application.Current.Dispatcher.Invoke(delegate {
						DownloadSizeFormatted = BytesToString(progress.TotalBytesToReceive);
					});
				});
		}

		public void Done()
		{
			Client.DownloadProgressChanged -= ProgressChanged;
		}

		private void ProgressChanged(object sender, DownloadProgressChangedEventArgs p)
		{
			_progress.OnNext(p);
		}

		static string BytesToString(long byteCount)
		{
			string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
			if (byteCount == 0) { 
				return "0 " + suf[0];
			}
			var bytes = Math.Abs(byteCount);
			var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
			var num = Math.Round(bytes / Math.Pow(1024, place), 1);
			return (Math.Sign(byteCount) * num) + " " + suf[place];
		}
	}
}
