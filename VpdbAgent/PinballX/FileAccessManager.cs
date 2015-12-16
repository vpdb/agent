using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IniParser;
using IniParser.Model;
using NLog;
using NuGet.Resolver;

namespace VpdbAgent.PinballX
{
	/// <summary>
	/// A class that abstracts all access to the file system.
	/// </summary>
	public interface IFileAccessManager
	{
		/// <summary>
		/// Parses a .ini file and returns the result or null if failed.
		/// </summary>
		/// <param name="path">Absolute path to .ini file</param>
		/// <returns>Parsed data or null if failed.</returns>
		IniData ParseIni(string path);
	}

	public class FileAccessManager : IFileAccessManager
	{
		// dependencies
		private readonly Logger _logger;

		public FileAccessManager(Logger logger)
		{
			_logger = logger;
		}

		public IniData ParseIni(string path)
		{
			if (File.Exists(path)) {
				var parser = new FileIniDataParser();
				return parser.ReadFile(path);
			}
			_logger.Error("Ini file at {0} does not exist.", path);
			return null;
		}
	}
}
