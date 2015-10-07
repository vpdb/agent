using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using ReactiveUI;

namespace VpdbAgent.ViewModels.TypeConverters
{
	
    public class BooleanToBrushTypeConverter : IBindingTypeConverter
	{
		public int GetAffinityForObjects(Type fromType, Type toType)
		{
			if (fromType == typeof(bool) && toType == typeof(Brush)) return 10;
			if (fromType == typeof(Brush) && toType == typeof(bool)) return 10;
			return 0;
		}

		public bool TryConvert(object from, Type toType, object conversionHint, out object result)
		{
			var hint = conversionHint is BooleanToBrushHint 
				? conversionHint as BooleanToBrushHint
				: new BooleanToBrushHint(Brushes.ForestGreen, Brushes.DarkRed);

			if (toType == typeof(Brush)) {
				var fromAsBool = (bool)from;
				result = fromAsBool ? hint.PositiveBrush : hint.NegativeBrush;
			} else {
				var fromAsBrush = (Brush)from;
				result = (fromAsBrush.Equals(hint.PositiveBrush));
			}
			return true;
		}
	}

	public class BooleanToBrushHint
	{
		public readonly Brush PositiveBrush;
		public readonly Brush NegativeBrush;
		public BooleanToBrushHint(Brush positiveBrush, Brush negativeBrush) {
			PositiveBrush = positiveBrush;
			NegativeBrush = negativeBrush;
		}
	}

}
