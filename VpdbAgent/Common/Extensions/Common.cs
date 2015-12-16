using System.Linq;

namespace VpdbAgent.Common.Extensions
{
	public static class Common
	{
		public static bool In<T>(this T val, params T[] values) where T : struct
		{
			return values.Contains(val);
		}
	}
}
