using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IniParser;
using IniParser.Model;
using Newtonsoft.Json;
using NLog;
using NuGet.Resolver;
using VpdbAgent.Application;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Network;

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

		/// <summary>
		/// Writes the database to the internal .json file for a given platform.
		/// </summary>
		/// <param name="database">Database file to marshal</param>
		/// <param name="databaseFile">Absolute path of the database file</param>
		void MarshallPlatformDatabase(PlatformDatabase database, string databaseFile);

		/// <summary>
		/// Reads the internal .json file of a given platform and returns the 
		/// unmarshalled database object.
		/// </summary>
		/// <param name="databaseFile">Absolute path of the database file to read</param>
		/// <returns>Deserialized object or empty database if no file exists or parsing error</returns>
		PlatformDatabase UnmarshallPlatformDatabase(string databaseFile);
	}

	public class FileAccessManager : IFileAccessManager
	{
		// dependencies
		private readonly Logger _logger;
		private readonly CrashManager _crashManager;

		public FileAccessManager(Logger logger, CrashManager crashManager)
		{
			_logger = logger;
			_crashManager = crashManager;
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

		private readonly JsonSerializer _serializer = new JsonSerializer {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new SnakeCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};

		public PlatformDatabase UnmarshallPlatformDatabase(string databaseFile)
		{
			if (!File.Exists(databaseFile)) {
				return new PlatformDatabase();
			}

			_logger.Info("Reading game database from {0}...", databaseFile);
			try {
				using (var sr = new StreamReader(databaseFile))
				using (JsonReader reader = new JsonTextReader(sr)) {
					try {
						var db = _serializer.Deserialize<PlatformDatabase>(reader);
						reader.Close();
						return db ?? new PlatformDatabase();
					} catch (Exception e) {
						_logger.Error(e, "Error parsing vpdb.json, deleting and ignoring.");
						_crashManager.Report(e, "json");
						reader.Close();
						File.Delete(databaseFile);
						return new PlatformDatabase();
					}
				}
			} catch (Exception e) {
				_logger.Error(e, "Error reading vpdb.json, deleting and ignoring.");
				_crashManager.Report(e, "json");
				return new PlatformDatabase();
			}
		}

		public void MarshallPlatformDatabase(PlatformDatabase database, string databaseFile)
		{
			// don't do anything for non-existent folder
			var dbFolder = Path.GetDirectoryName(databaseFile);
			if (dbFolder != null && databaseFile != null && !Directory.Exists(dbFolder)) {
				_logger.Warn("Directory {0} does not exist, not writing vpdb.json.", dbFolder);
				return;
			}

			try {
				using (var sw = new StreamWriter(databaseFile))
				using (JsonWriter writer = new JsonTextWriter(sw)) {
					_serializer.Serialize(writer, database);
				}
				_logger.Debug("Wrote vpdb.json back to {0}", databaseFile);
			} catch (Exception e) {
				_logger.Error(e, "Error writing vpdb.json to {0}", databaseFile);
				_crashManager.Report(e, "json");
			}
		}
	}
}
