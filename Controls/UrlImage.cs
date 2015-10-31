using System;
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NLog;
using Splat;
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
				Logger.Info("Loading image {0}...", value);
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

			// if cached, set from cache
			if (IsCached(urlSource)) {
				Opacity = 1;
				Source = new BitmapImage(new Uri(GetLocalPath(urlSource)));
				return;
			}

			// remote, so make it transparent for fading animation
			Opacity = 0;

			// download
			var webRequest = VpdbClient.GetWebRequest(urlSource);
			webRequest.BeginGetResponse(ar =>
			{
				try {
					var response = webRequest.EndGetResponse(ar);
					var stream = response.GetResponseStream();
					if (stream != null && !stream.CanRead) {
						return;
					}
					var buffer = new byte[response.ContentLength];
					stream?.BeginRead(buffer, 0, buffer.Length, aResult =>
					{
						stream.EndRead(aResult);
						var image = new BitmapImage();
						image.BeginInit();
						image.StreamSource = new MemoryStream(buffer);
						image.EndInit();
						image.Freeze();
						Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
							Cache(urlSource, buffer);
							var da = new DoubleAnimation {
								From = 0,
								To = 1,
								Duration = new Duration(TimeSpan.FromMilliseconds(200))
							};
							Source = image;
							BeginAnimation(OpacityProperty, da);
						}));
					}, null);

				} catch (Exception e) {
					Logger.Warn("Error loading image {0}: {1}", webRequest.RequestUri.AbsoluteUri, e.Message);
				}
			}, null);
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
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"VpdbAgent",
				"Cache",
				path.Replace("/", @"\").Substring(1)
			);
		}

		/// <summary>
		/// Saves an image to the cache
		/// </summary>
		/// <param name="path">Absolute remote path without host and schema</param>
		/// <param name="bytes">Downloaded image</param>
		private static void Cache(string path, byte[] bytes)
		{
			var localPath = GetLocalPath(path);
			try {
				
				var localDir = Path.GetDirectoryName(localPath);
				if (localDir != null && !Directory.Exists(localDir)) {
					Directory.CreateDirectory(localDir);
				}
				File.WriteAllBytes(localPath, bytes);

			} catch (Exception e) {
				Logger.Error(e, "Error writing cache image to {0}", localPath);
			}
		}
	}
}
