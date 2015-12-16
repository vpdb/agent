using System.Windows;

namespace VpdbAgent.Common.TypeConverters
{
	public sealed class BooleanToVisibilityConverter : BooleanConverter<Visibility>
	{
		public BooleanToVisibilityConverter() : base(Visibility.Visible, Visibility.Collapsed) { }
	}
}
