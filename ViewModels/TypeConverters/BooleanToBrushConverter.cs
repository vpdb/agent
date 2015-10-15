using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace VpdbAgent.ViewModels.TypeConverters
{
	public sealed class BooleanToBrushConverter : BooleanConverter<Brush>
	{
		public BooleanToBrushConverter() : base(Brushes.ForestGreen, Brushes.DarkRed) { }
	}
}
