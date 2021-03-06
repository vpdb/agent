﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace VpdbAgent.Common.ValueConverters
{
	[ExcludeFromCodeCoverage]
	[ValueConversion(typeof(bool), typeof(bool))]
	public class InverseBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (targetType != typeof(bool)) {
				throw new InvalidOperationException("The target must be a boolean");
			}

			return !(bool)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (targetType != typeof(bool)) {
				throw new InvalidOperationException("The target must be a boolean");
			}
			return !(bool)value;
		}
	}
}
