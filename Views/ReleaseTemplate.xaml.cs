using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using VpdbAgent.Vpdb.Network;

namespace VpdbAgent.Pages
{
	/// <summary>
	/// Interaction logic for ReleaseTemplate.xaml
	/// </summary>
	public partial class ReleaseTemplate : UserControl
	{

		public static readonly DependencyProperty ReleaseProperty = DependencyProperty.Register("Release", typeof(Release), typeof(ReleaseTemplate), new PropertyMetadata(default(Release), ReleasePropertyChanged));

		static void ReleasePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var source = d as ReleaseTemplate;
			source.bind();
		}

		public Release Release
		{
			get { return GetValue(ReleaseProperty) as Release; }
			set
			{
				SetValue(ReleaseProperty, value);
			}
		}

		public ReleaseTemplate()
		{
			InitializeComponent();
		}

		public void bind()
		{
			getImageFromURL(Release.LatestVersion.Thumb.Image.Url);
		}

		private void getImageFromURL(string path)
		{
			thumb.Opacity = 0;
			thumb.Source = null;
			var webRequest = VpdbClient.GetInstance().GetWebRequest(path);
			webRequest.BeginGetResponse((ar) =>
			{
				try {
					var response = webRequest.EndGetResponse(ar);
					var stream = response.GetResponseStream();
					if (stream.CanRead) {
						Byte[] buffer = new Byte[response.ContentLength];
						stream.BeginRead(buffer, 0, buffer.Length, (aResult) =>
						{
							stream.EndRead(aResult);
							BitmapImage image = new BitmapImage();
							image.BeginInit();
							image.StreamSource = new MemoryStream(buffer);
							image.EndInit();
							image.Freeze();
							this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate {

								DoubleAnimation da = new DoubleAnimation();
								da.From = 0;
								da.To = 1;
								da.Duration = new Duration(TimeSpan.FromMilliseconds(200));
								thumb.Source = image;
								thumb.BeginAnimation(OpacityProperty, da);
							}));
						}, null);
					}
				} catch (Exception e) {
					Console.WriteLine("Error loading image {0}: {1}", webRequest.RequestUri.AbsoluteUri, e.Message);
				}
			}, null);
		}

	}
}
