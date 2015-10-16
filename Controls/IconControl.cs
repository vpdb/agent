using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VpdbAgent.Common;


namespace VpdbAgent.Controls
{
	/// <summary>
	/// Displays a vector icon.
	/// </summary>
	public class IconControl : Control
	{
		public static readonly DependencyProperty DataGeometryProperty =
			DependencyProperty.Register("DataGeometry", typeof(PathGeometry), typeof(IconControl), new PropertyMetadata(null));

		public static readonly DependencyProperty DataProperty =
			DependencyProperty.Register("Data", typeof(string), typeof(IconControl), new PropertyMetadata(null, OnDataChanged));

		public IconControl()
		{
			DefaultStyleKey = typeof(IconControl);
		}

		// Write-only to be used in a binding.
		public string Data
		{
			private get { return (string)GetValue(DataProperty); }
			set { SetValue(DataProperty, value); }
		}

		// Read-only to be used in the control's template.
		public PathGeometry DataGeometry
		{
			get { return (PathGeometry)GetValue(DataGeometryProperty); }
			private set { SetValue(DataGeometryProperty, value); }
		}

		private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var ic = d as IconControl;

			if (ic != null) {
				ic.DataGeometry = PathGeometry.CreateFromGeometry(Geometry.Parse(e.NewValue.ToString()));
			}
		}
	}
}