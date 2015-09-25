using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NLog;
using VpdbAgent.Vpdb;

namespace VpdbAgent.Common
{
	public class ImageUtils
	{
		private static ImageUtils _instance;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly VpdbClient _vpdbClient = VpdbClient.GetInstance();

		private ImageUtils() { }

		public void LoadImage(string path, System.Windows.Controls.Image imageView, Dispatcher dispatcher)
		{
			imageView.Opacity = 0;
			imageView.Source = null;
			var webRequest = _vpdbClient.GetWebRequest(path);
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
						var image = new BitmapImage();
						image.BeginInit();
						image.StreamSource = new MemoryStream(buffer);
						image.EndInit();
						image.Freeze();
						dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {

							var da = new DoubleAnimation {
								From = 0,
								To = 1,
								Duration = new Duration(TimeSpan.FromMilliseconds(200))
							};
							imageView.Source = image;
							imageView.BeginAnimation(UIElement.OpacityProperty, da);
						}));
					}, null);
				} catch (Exception e) {
					Logger.Warn("Error loading image {0}: {1}", webRequest.RequestUri.AbsoluteUri, e.Message);
				}
			}, null);
		}

		public static ImageUtils GetInstance()
		{
			return _instance ?? (_instance = new ImageUtils());
		}
	}
}
