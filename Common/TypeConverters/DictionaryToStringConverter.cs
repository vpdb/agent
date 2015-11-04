using System;
using System.Collections.Generic;
using System.Windows;
using ReactiveUI;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Common.TypeConverters
{
	public class DictionaryToStringConverter : IBindingTypeConverter
	{
		public int GetAffinityForObjects(Type fromType, Type toType)
		{
			if (fromType == typeof(Dictionary<string, string>)) {
				return 1000;
			}
			return -1;
		}

		public bool TryConvert(object from, Type toType, object conversionHint, out object result)
		{
			if (conversionHint == null) {
				result = null;
				return false;
			}
			var key = (string)conversionHint;
			var dict = from as Dictionary<string, string>;
			if (dict != null && dict.ContainsKey(key) && dict[key] != null) {
				result = dict[key];
				return true;
			};

			result = null;
			return false;
		}
	}
}
