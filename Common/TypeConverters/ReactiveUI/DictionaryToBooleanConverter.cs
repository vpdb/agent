using System;
using System.Collections.Generic;
using ReactiveUI;

namespace VpdbAgent.Common.TypeConverters.ReactiveUI
{
	public class DictionaryToBooleanConverter : IBindingTypeConverter
	{
		public int GetAffinityForObjects(Type fromType, Type toType)
		{
			if (fromType == typeof(Dictionary<string, string>) && toType == typeof(bool)) return 1100;
			return -1;
		}

		public bool TryConvert(object from, Type toType, object conversionHint, out object result)
		{
			if (conversionHint == null) {
				throw new InvalidOperationException("You must supply the key to check against in the conversion hint.");
			}
			var key = (string) conversionHint;
			if (toType == typeof(bool)) {
				var dict = (Dictionary<string, string>) from;
				result = dict != null && dict.ContainsKey(key) && dict[key] != null;
				return true;
			}
			result = null;
			return false;
		}
	}
}
