using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json.Bson;
using VpdbAgent.Common;

namespace VpdbAgent.Controls
{
	/// <summary>
	/// An image that can be loaded by an URL.
	/// </summary>
	public class UrlImage : Image
	{
		static UrlImage() { }

		private static readonly ImageUtils ImageUtils = ImageUtils.GetInstance();

		public static readonly DependencyProperty UrlSourceProperty = 
			DependencyProperty.Register("UrlSource", typeof(string), typeof(UrlImage), new FrameworkPropertyMetadata(string.Empty, UrlSourcePropertyChanged));

		static void UrlSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var source = d as UrlImage;
			source?.Bind();
		}

		public string UrlSource
		{
			get { return (string)GetValue(UrlSourceProperty); }
			set { SetValue(UrlSourceProperty, value); }
		}

		private void Bind()
		{
			ImageUtils.LoadImage(UrlSource, this, Dispatcher);
		}
	}
}
