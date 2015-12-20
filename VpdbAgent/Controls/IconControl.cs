using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VpdbAgent.Controls
{
	/// <summary>
	/// Displays a vector icon.
	/// </summary>
	[ExcludeFromCodeCoverage]
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

			if (ic != null && e.NewValue != null) {
				try {
					ic.DataGeometry = PathGeometry.CreateFromGeometry(Geometry.Parse(e.NewValue.ToString()));
				} catch (Exception error) {
					Console.WriteLine(error);
				}
			}
		}
	}
}