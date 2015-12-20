using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace VpdbAgent.Common.TypeConverters
{
	[ExcludeFromCodeCoverage]
	public sealed class BooleanToVisibilityConverter : BooleanConverter<Visibility>
	{
		public BooleanToVisibilityConverter() : base(Visibility.Visible, Visibility.Collapsed) { }
	}
}
