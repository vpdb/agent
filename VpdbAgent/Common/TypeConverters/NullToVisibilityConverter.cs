using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace VpdbAgent.Common.TypeConverters
{
	[ExcludeFromCodeCoverage]
	public sealed class NullToVisibilityConverter : NullConverter<Visibility>
	{
		public NullToVisibilityConverter() : base(Visibility.Visible, Visibility.Collapsed) { }
	}
}
