
using System;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Akavache;
using NLog;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Common.Extensions;
using VpdbAgent.Vpdb;

namespace VpdbAgent.Controls
{
	public class CachedImage : Image
	{
		// dependencies
		private static readonly IVpdbClient VpdbClient = Locator.Current.GetService<IVpdbClient>();
		private static readonly Logger Logger = Locator.Current.GetService<Logger>();
		private static readonly IBlobCache Storage = BlobCache.LocalMachine;

		static CachedImage()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(CachedImage), new FrameworkPropertyMetadata(typeof(CachedImage)));
		}

		public readonly static DependencyProperty ImageUrlProperty = DependencyProperty.Register("ImageUrl", typeof(string), typeof(CachedImage), new PropertyMetadata("", ImageUrlPropertyChanged));

		public string ImageUrl
		{
			get { return (string)GetValue(ImageUrlProperty); }
			set { SetValue(ImageUrlProperty, value); }
		}

		private static void ImageUrlPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
		{
			var url = (string)e.NewValue;

			// clear if nothing set
			if (string.IsNullOrEmpty(url)) {
				SetSource((CachedImage)obj, null);
				return;
			}

			Storage.LoadImageFromVpdb(url).Subscribe(bmp => {
				System.Windows.Application.Current.Dispatcher.Invoke(delegate {
					((CachedImage)obj).Source = bmp.ToNative();
				});
			}, err => {
				Logger.Error(err, "Error downloading {0}.", url);
			});

			/*var localPath = GetLocalPath(url);
			if (IsCached(url)) {
				SetSource((CachedImage)obj, localPath);

			} else {
				var webClient = VpdbClient.GetWebClient();
				var uri = VpdbClient.GetUri(url);

				webClient.DownloadFileCompleted += (sender, args) => {
					if (args.Error != null) {
						File.Delete(localPath);
						return;
					}

					SetSource((CachedImage)obj, localPath, true);
				};

				MakeLocalPath(url);
				Logger.Info("Downloading image from {0}", uri.ToString());
				webClient.DownloadFileAsync(uri, localPath);
			}*/
		}

		private static void SetSource(Image img, string path, bool fadeIn = false)
		{
			if (fadeIn) {
				img.Opacity = 0;
			}

			img.Source = path != null ? new BitmapImage(new Uri(path)) : null;

			if (fadeIn) {
				var da = new DoubleAnimation {
					From = 0,
					To = 1,
					Duration = new Duration(TimeSpan.FromMilliseconds(300))
				};
				img.BeginAnimation(OpacityProperty, da);
			}
		}

	}
}
