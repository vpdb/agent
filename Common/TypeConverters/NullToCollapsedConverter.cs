using System;
using System.Windows;
using ReactiveUI;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Common.TypeConverters
{
	public class NullToCollapsedConverter : IBindingTypeConverter
	{
		public int GetAffinityForObjects(Type fromType, Type toType)
		{
			if (toType == typeof(Visibility)) return 100;
			return -1;
		}

		public bool TryConvert(object from, Type toType, object conversionHint, out object result)
		{
			if (toType == typeof(Visibility)) {
				result = from == null ? Visibility.Collapsed : Visibility.Visible;
				return true;
			}
			result = null;
			return false;
		}
	}
}
