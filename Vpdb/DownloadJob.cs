using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using VpdbAgent.Vpdb.Models;
using File = VpdbAgent.Vpdb.Models.File;

namespace VpdbAgent.Vpdb
{
	public class DownloadJob
	{
		public readonly Uri Uri;
		public readonly string Filename;
		public readonly WebClient Client;
		public IObservable<DownloadProgressChangedEventArgs> WhenDownloadProgresses => _progress;

		private readonly Subject<DownloadProgressChangedEventArgs> _progress = new Subject<DownloadProgressChangedEventArgs>();

		public DownloadJob(Release release, File file, IVpdbClient client)
		{
			Uri = client.GetUri(file.Reference.Url);
			Client = client.GetWebClient();
			Filename = file.Reference.Name;

			var progress = Observable.FromEvent<DownloadProgressChangedEventHandler, DownloadProgressChangedEventArgs>(
				handler => Client.DownloadProgressChanged += handler,
				handler => Client.DownloadProgressChanged -= handler);
			/*
			progress.Subscribe(p =>
			{
				Console.WriteLine("Progress: {0}", p.BytesReceived);
			});*/
		}
	}
}
