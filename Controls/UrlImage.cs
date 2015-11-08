using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NLog;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Vpdb;

namespace VpdbAgent.Controls
{
	/// <summary>
	/// An image that can be loaded by an URL.
	/// 
	/// Includes primitive caching (no date check, just checks if image is in
	/// cache folder).
	/// </summary>
	public class UrlImage : Image
	{
		static UrlImage() { }

		// dependencies
		private static readonly IVpdbClient VpdbClient = Locator.Current.GetService<IVpdbClient>();
		private static readonly Logger Logger = Locator.Current.GetService<Logger>();

		public static readonly DependencyProperty UrlSourceProperty =
			DependencyProperty.Register("UrlSource", typeof(string), typeof(UrlImage), new FrameworkPropertyMetadata(string.Empty, Changed));

		private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.Property.Equals(UrlSourceProperty)) {
				var image = d as UrlImage;
				image?.LoadImage();
			}
		}

		public string UrlSource
		{
			get { return (string)GetValue(UrlSourceProperty); }
			set
			{
				SetValue(UrlSourceProperty, value);
				LoadImage();
			}
		}

		/// <summary>
		/// If not cached, downloads and fades-in the image. Otherwise just loads
		/// it into the view.
		/// </summary>
		private void LoadImage()
		{
			// reset image
			Source = null;
			var urlSource = UrlSource;

			// if not set, ignore
			if (string.IsNullOrEmpty(urlSource)) {
				return;
			}

			// in design mode, ignore
			if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) {
				return;
			}

			// on worker thread
			Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
			{
				var localPath = GetLocalPath(urlSource);

				// if cached, set from cache
				if (IsCached(urlSource)) {
					Opacity = 1;
					Source = new BitmapImage(new Uri(localPath));
					return;
				}

				// remote, so make it transparent for fading animation
				Opacity = 0;

				// download
				var webClient = VpdbClient.GetWebClient();
				var uri = VpdbClient.GetUri(urlSource);
				MakeLocalPath(urlSource);
				Logger.Info("Downloading image from {0}", uri.ToString());
				webClient.DownloadFile(uri, localPath);

				// animate into view
				Source = new BitmapImage(new Uri(localPath));
				var da = new DoubleAnimation {
					From = 0,
					To = 1,
					Duration = new Duration(TimeSpan.FromMilliseconds(300))
				};
				BeginAnimation(OpacityProperty, da);
			}));
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
