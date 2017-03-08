using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Common.Filesystem
{
	public class PathHelper
	{
		/// <summary>
		/// Normalizes the given path. Directories don't end with trailing slash.
		/// </summary>
		/// <param name="path">Path to normalize</param>
		/// <returns>Normalized path</returns>
		public static string NormalizePath(string path)
		{
			return Path.GetFullPath(new Uri(path).LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}
	}
}
