using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows.Data;

namespace VpdbAgent.Common.TypeConverters
{
	[ExcludeFromCodeCoverage]
	public class NullConverter<T> : IValueConverter
	{
		public NullConverter(T trueValue, T falseValue)
		{
			NotNull = trueValue;
			Null = falseValue;
		}

		public T NotNull { get; set; }
		public T Null { get; set; }

		public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value != null ? NotNull : Null;
		}

		public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is T && EqualityComparer<T>.Default.Equals((T)value, NotNull);
		}
	}
}