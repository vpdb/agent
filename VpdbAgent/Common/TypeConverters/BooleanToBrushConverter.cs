using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;

namespace VpdbAgent.Common.TypeConverters
{
	[ExcludeFromCodeCoverage]
	public sealed class BooleanToBrushConverter : BooleanConverter<Brush>
	{
		public BooleanToBrushConverter() : base(Brushes.ForestGreen, Brushes.DarkRed) { }
	}
}
