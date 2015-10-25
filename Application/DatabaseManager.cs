using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Network;

namespace VpdbAgent.Application
{

	public interface IDatabaseManager
	{
		GlobalDatabase Database { get; }
		IDatabaseManager Initialize();
		IDatabaseManager Save();
	}

	public class DatabaseManager : IDatabaseManager
	{
		// deps
		private readonly ISettingsManager _settingsManager;
		private readonly Logger _logger;

		// props
		public GlobalDatabase Database { get; private set; }

		private string _dbPath;

		public DatabaseManager(ISettingsManager settingsManager, Logger logger)
		{
			_settingsManager = settingsManager;
			_logger = logger;
		}

		public IDatabaseManager Initialize()
		{
			_dbPath = Path.Combine(_settingsManager.PbxFolder, @"Databases\vpdb.json");
			Database = UnmarshallDatabase();
			_logger.Info("Global database with {0} release(s) loaded.", Database.Releases.Count);

			return this;
		}

		public IDatabaseManager Save()
		{
			MarshallDatabase();
			return this;
		}


		/// <summary>
		/// Reads the internal global .json file of a given platform and 
		/// returns the unmarshalled database object.
		/// </summary>
		/// <returns>Deserialized object or empty database if no file exists or parsing error</returns>
		private GlobalDatabase UnmarshallDatabase()
		{
			if (!File.Exists(_dbPath)) {
				_logger.Info("Creating new global database, {0} not found.", _dbPath);
				return new GlobalDatabase();
			}
			try {
				using (var sr = new StreamReader(_dbPath))
				using (JsonReader reader = new JsonTextReader(sr)) {
					try {
						var db = _serializer.Deserialize<GlobalDatabase>(reader);
						reader.Close();
						return db ?? new GlobalDatabase();
					} catch (Exception e) {
						_logger.Error(e, "Error parsing {0}, deleting and ignoring.", _dbPath);
						reader.Close();
						File.Delete(_dbPath);
						return new GlobalDatabase();
					}
				}
			} catch (Exception e) {
				_logger.Error(e, "Error reading {0}, deleting and ignoring.", _dbPath);
				return new GlobalDatabase();
			}
		}

		/// <summary>
		/// Writes the database to the internal .json file for the global database.
		/// </summary>
		private void MarshallDatabase()
		{
			var dbFolder = Path.GetDirectoryName(_dbPath);
			if (dbFolder != null && !Directory.Exists(dbFolder)) {
				_logger.Warn("Directory {0} does not exist, not writing vpdb.json.", dbFolder);
				return;
			}

			try {
				using (var sw = new StreamWriter(_dbPath))
				using (JsonWriter writer = new JsonTextWriter(sw)) {
					_serializer.Serialize(writer, Database);
				}
				_logger.Debug("Wrote vpdb.json back to {0}", _dbPath);
			} catch (Exception e) {
				_logger.Error(e, "Error writing database to {0}", _dbPath);
			}
		}


		// final
		private readonly JsonSerializer _serializer = new JsonSerializer {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new SnakeCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};
	}
}
