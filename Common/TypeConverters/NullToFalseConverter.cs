using System;
using System.Windows;
using ReactiveUI;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Common.TypeConverters
{
	public class NullToFalseConverter : IBindingTypeConverter
	{
		public int GetAffinityForObjects(Type fromType, Type toType)
		{
			if (toType == typeof(bool)) return 100;
			return -1;
		}

		public bool TryConvert(object from, Type toType, object conversionHint, out object result)
		{
			if (toType == typeof(bool)) {
				result = @from != null;
				return true;
			}
			result = null;
			return false;
		}
	}
}
