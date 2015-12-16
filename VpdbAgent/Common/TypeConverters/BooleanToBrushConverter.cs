using System.Windows.Media;

namespace VpdbAgent.Common.TypeConverters
{
	public sealed class BooleanToBrushConverter : BooleanConverter<Brush>
	{
		public BooleanToBrushConverter() : base(Brushes.ForestGreen, Brushes.DarkRed) { }
	}
}
