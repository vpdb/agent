
using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using NLog;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Vpdb;

namespace VpdbAgent.Controls
{
	public class CachedImage : Image
	{
		// dependencies
		private static readonly IVpdbClient VpdbClient = Locator.Current.GetService<IVpdbClient>();
		private static readonly Logger Logger = Locator.Current.GetService<Logger>();

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
			if (string.IsNullOrEmpty(url)) {
				SetSource((CachedImage)obj, null);
				return;
			}

			var localPath = GetLocalPath(url);
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
			}
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


		/// <summary>
		/// Checks if the image is locally cached.
		/// </summary>
		/// <param name="path">Absolute remote path without host and schema</param>
		/// <returns>True if cached, False otherwise.</returns>
		private static bool IsCached(string path)
		{
			return File.Exists(GetLocalPath(path));
		}

		/// <summary>
		/// Returns the local cache path on the disk based on the URL path of
		/// the image.
		/// </summary>
		/// <param name="path">Absolute remote path without host and schema</param>
		/// <returns></returns>
		private static string GetLocalPath(string path)
		{
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				SettingsManager.DataFolder,
				"Cache",
				path.Replace("/", @"\").Substring(1)
			);
		}

		/// <summary>
		/// Creates the cache folder of the path to cache.
		/// </summary>
		/// <param name="path">Absolute remote path without host and schema</param>
		private static void MakeLocalPath(string path)
		{
			var localDir = Path.GetDirectoryName(GetLocalPath(path));
			if (localDir != null && !Directory.Exists(localDir)) {
				Directory.CreateDirectory(localDir);
			}
		}
	}
}
