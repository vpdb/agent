using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml.Serialization;
using IniParser;
using IniParser.Model;
using Newtonsoft.Json;
using NLog;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.Data;
using VpdbAgent.Models;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Network;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace VpdbAgent.PinballX
{
	/// <summary>
	/// A class that abstracts all serializing access to the file system.
	/// </summary>
	/// <remarks>
	/// Note that this is currently mocked in tests, but could be included 
	/// in the future.
	/// </remarks>
	public interface IMarshallManager
	{
		/// <summary>
		/// Parses a .ini file and returns the result or null if failed.
		/// </summary>
		/// <param name="path">Absolute path to .ini file</param>
		/// <returns>Parsed data or null if failed.</returns>
		IniData ParseIni(string path);
		
		/// <summary>
		/// Writes the data to the internal .json file for a given platform.
		/// </summary>
		/// <param name="data">Data file to marshal</param>
		/// <param name="dataFile">Absolute path to the data file</param>
		void MarshallMappings(SystemMapping data, string dataFile);

		/// <summary>
		/// Reads the internal .json file of a given platform and returns the 
		/// unmarshalled data object.
		/// </summary>
		/// <param name="databaseFile">Absolute path of the data file to read</param>
		/// <param name="system">The system of the mappings</param>
		/// <returns>Deserialized object or empty data if no file exists or parsing error</returns>
		SystemMapping UnmarshallMappings(string databaseFile, PinballXSystem system);

		/// <summary>
		/// Returns an unmarshalled object for a given .XML file
		/// </summary>
		/// <param name="filepath">Absolute path to the .XML file</param>
		/// <returns></returns>
		PinballXMenu UnmarshallXml(string filepath);

		/// <summary>
		/// Saves the menu back to the XML file.
		/// </summary>
		/// <remarks>
		/// This should only be used for updating or adding games by VPDB Agent,
		/// i.e. those in Vpdb.xml that is managed by VPDB Agent. For existing games
		/// another serializer should be used that keeps eventual comments and
		/// ordering intact.
		/// </remarks>
		/// <param name="menu"></param>
		/// <param name="filepath"></param>
		void MarshallXml(PinballXMenu menu, string filepath);
	}

	[ExcludeFromCodeCoverage]
	public class MarshallManager : IMarshallManager
	{
		// dependencies
		private readonly ILogger _logger;
		private readonly IFile _file;
		private readonly CrashManager _crashManager;

		public MarshallManager(ILogger logger, IFile file, CrashManager crashManager)
		{
			_logger = logger;
			_file = file;
			_crashManager = crashManager;
		}

		public IniData ParseIni(string path)
		{
			if (_file.Exists(path)) {
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

		public SystemMapping UnmarshallMappings(string databaseFile, PinballXSystem system)
		{
			if (!_file.Exists(databaseFile)) {
				return new SystemMapping();
			}

			_logger.Info("Reading platform data from {0}...", databaseFile);
			try {
				using (var sr = new StreamReader(databaseFile))
				using (JsonReader reader = new JsonTextReader(sr)) {
					try {
						var db = _serializer.Deserialize<SystemMapping>(reader);
						db.System = system;
						reader.Close();
						return db ?? new SystemMapping();
					} catch (Exception e) {
						_logger.Error(e, "Error parsing vpdb.json, deleting and ignoring.");
						_crashManager.Report(e, "json");
						reader.Close();
						File.Delete(databaseFile);
						return new SystemMapping();
					}
				}
			} catch (Exception e) {
				_logger.Error(e, "Error reading vpdb.json, deleting and ignoring.");
				_crashManager.Report(e, "json");
				return new SystemMapping();
			}
		}

		public void MarshallMappings(SystemMapping data, string dataFile)
		{
			// don't do anything for non-existent folder
			var dbFolder = Path.GetDirectoryName(dataFile);
			if (dbFolder != null && dataFile != null && !Directory.Exists(dbFolder)) {
				_logger.Warn("Directory {0} does not exist, not writing vpdb.json.", dbFolder);
				return;
			}

			try {
				using (var sw = new StreamWriter(dataFile))
				using (JsonWriter writer = new JsonTextWriter(sw)) {
					_serializer.Serialize(writer, data);
				}
				_logger.Debug("Wrote vpdb.json back to {0}", dataFile);
			} catch (Exception e) {
				_logger.Error(e, "Error writing vpdb.json to {0}", dataFile);
				_crashManager.Report(e, "json");
			}
		}

		public PinballXMenu UnmarshallXml(string filepath)
		{
			var menu = new PinballXMenu();

			if (!_file.Exists(filepath)) {
				return menu;
			}
			Stream reader = null;
			try {
				var serializer = new XmlSerializer(typeof(PinballXMenu));
				reader = new FileStream(filepath, FileMode.Open);
				menu = serializer.Deserialize(reader) as PinballXMenu;

			} catch (Exception e) {
				_logger.Error(e, "Error parsing {0}: {1}", filepath, e.Message);
				_crashManager.Report(e, "xml");

			} finally {
				reader?.Close();
			}
			return menu;
		}

		public void MarshallXml(PinballXMenu menu, string filepath)
		{
			try {
				var serializer = new XmlSerializer(typeof(PinballXMenu));
				using (TextWriter writer = new StreamWriter(filepath)) {
					serializer.Serialize(writer, menu);
					_logger.Info("Saved {0}.", filepath);
				}
			} catch (Exception e) {
				_logger.Error(e, "Error writing XML to {0}: {1}", filepath, e.Message);
				_crashManager.Report(e, "xml");
			}
		}
	}
}
