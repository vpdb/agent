using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Akavache;
using NLog;
using Splat;
using VpdbAgent.Common.Extensions;
using ILogger = NLog.ILogger;

namespace VpdbAgent.Controls
{
	[ExcludeFromCodeCoverage]
	public class CachedImage : Image
	{
		// constants
		private static readonly TimeSpan FadeInDuration = TimeSpan.FromMilliseconds(300);
		private static readonly TimeSpan WaitForFadeInDuration = TimeSpan.FromMilliseconds(100);
		private static readonly DoubleAnimation FadeInAnimation = new DoubleAnimation {
			From = 0,
			To = 1,
			Duration = new Duration(FadeInDuration)
		};

		// dependencies
		private static readonly ILogger Logger = Locator.Current.GetService<ILogger>();
		private static readonly IBlobCache Storage = BlobCache.LocalMachine;

		// members
		public bool Animate;

		#region ImageUrl Property
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
		#endregion

		private static void ImageUrlPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
		{
			var url = (string)e.NewValue;
			var image = (CachedImage) obj;

			// clear if nothing set
			if (string.IsNullOrEmpty(url)) {
				image.Source = null;
				return;
			}

			image.Animate = false;
			Storage.LoadImageFromVpdb(url).Subscribe(bmp => {
				// process on main thread
				System.Windows.Application.Current.Dispatcher.Invoke(delegate {
					if (image.Animate) {
						image.Opacity = 0;
					}
					image.Source = bmp.ToNative();
					if (image.Animate) {
						image.BeginAnimation(OpacityProperty, FadeInAnimation);
					}
				});
			}, err => {
				Logger.Error(err, "Error downloading {0}.", url);
			});
			Observable.Timer(WaitForFadeInDuration).Subscribe(_ => image.Animate = true);
		}
	}
}
