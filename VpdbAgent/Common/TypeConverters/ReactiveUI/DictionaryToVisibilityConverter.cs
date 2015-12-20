using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using ReactiveUI;

namespace VpdbAgent.Common.TypeConverters.ReactiveUI
{
	[ExcludeFromCodeCoverage]
	public class DictionaryToVisibilityConverter : IBindingTypeConverter
	{
		public int GetAffinityForObjects(Type fromType, Type toType)
		{
			if (fromType == typeof(Dictionary<string, string>) && toType == typeof(Visibility)) return 1100;
			return -1;
		}

		public bool TryConvert(object from, Type toType, object conversionHint, out object result)
		{
			if (conversionHint == null) {
				throw new InvalidOperationException("You must supply the key to check against in the conversion hint.");
			}
			var key = (string) conversionHint;
			if (toType == typeof(Visibility)) {
				var dict = (Dictionary<string, string>) from;
				result = from != null && dict.ContainsKey(key) && dict[key] != null ? Visibility.Visible: Visibility.Collapsed;
				return true;
			}
			result = null;
			return false;
		}
	}
}
