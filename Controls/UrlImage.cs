using System;
using System.IO;
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
			DependencyProperty.Register("UrlSource", typeof(string), typeof(UrlImage), new FrameworkPropertyMetadata(string.Empty));

		public string UrlSource
		{
			get { return (string)GetValue(UrlSourceProperty); }
			set { 
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
			// if not set, ignore
			if (UrlSource == null) {
				Logger.Warn("Ignoring null-image.");
				return;
			}

			// if cached, set from cache
			if (IsCached(UrlSource)) {
				this.Source = new BitmapImage(new Uri(GetLocalPath(UrlSource)));
				return;
			}

			// remote, so make it transparent for fading animation
			this.Opacity = 0;
			this.Source = null;

			// download
			var webRequest = VpdbClient.GetWebRequest(UrlSource);
			webRequest.BeginGetResponse((ar) =>
			{
				try {
					var response = webRequest.EndGetResponse(ar);
					var stream = response.GetResponseStream();
					if (stream != null && !stream.CanRead) {
						return;
					}
					var buffer = new byte[response.ContentLength];
					stream?.BeginRead(buffer, 0, buffer.Length, (aResult) =>
					{
						stream.EndRead(aResult);
						Cache(UrlSource, buffer);
						var image = new BitmapImage();
						image.BeginInit();
						image.StreamSource = new MemoryStream(buffer);
						image.EndInit();
						image.Freeze();
						Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {
							var da = new DoubleAnimation {
								From = 0,
								To = 1,
								Duration = new Duration(TimeSpan.FromMilliseconds(200))
							};
							this.Source = image;
							this.BeginAnimation(UIElement.OpacityProperty, da);
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
			if (!Directory.Exists(Path.GetDirectoryName(localPath))) {
				Directory.CreateDirectory(Path.GetDirectoryName(localPath));
			}
			File.WriteAllBytes(localPath, bytes);
		}
	}
}
