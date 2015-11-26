using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Models;
using VpdbAgent.Vpdb.Network;
using File = System.IO.File;

namespace VpdbAgent.Application
{
	/// <summary>
	/// Manages the global stuff we're saving to disk.
	/// 
	/// Currently, it's serialized as a .json file in PinballX's database root
	/// folder. Might be switching it out for something more efficient such as
	/// protocol-buffers.
	/// </summary>
	/// <see cref="GlobalDatabase"/>
	public interface IDatabaseManager
	{
		/// <summary>
		/// Retrieve the global database
		/// </summary>
		GlobalDatabase Database { get; }

		/// <summary>
		/// Read data, run when settings are initialized.
		/// </summary>
		/// <returns></returns>
		IDatabaseManager Initialize();

		/// <summary>
		/// Saves data back to disk
		/// </summary>
		/// <returns></returns>
		IDatabaseManager Save();

		IDatabaseManager Log(Message msg);
	}

	public class DatabaseManager : IDatabaseManager
	{
		// deps
		private readonly ISettingsManager _settingsManager;
		private readonly Logger _logger;

		// props
		public GlobalDatabase Database { get; } = new GlobalDatabase();

		private string _dbPath;

		public DatabaseManager(ISettingsManager settingsManager, Logger logger)
		{
			_settingsManager = settingsManager;
			_logger = logger;
		}

		public IDatabaseManager Initialize()
		{
			_dbPath = Path.Combine(_settingsManager.Settings.PbxFolder, @"Databases\vpdb.json");
			Database.Update(UnmarshallDatabase());
			_logger.Info("Global database with {0} release(s) loaded.", Database.Releases.Count);

			return this;
		}

		public IDatabaseManager Save()
		{
			MarshallDatabase();
			return this;
		}

		public IDatabaseManager Log(Message msg)
		{
			Database.Messages.Add(msg);
			return Save();
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
				return null;
			}
			try {
				using (var sr = new StreamReader(_dbPath))
				using (JsonReader reader = new JsonTextReader(sr)) {
					try {
						var db = _serializer.Deserialize<GlobalDatabase>(reader);
						reader.Close();
						return db;
					} catch (Exception e) {
						_logger.Error(e, "Error parsing {0}, deleting and ignoring.", _dbPath);
						reader.Close();
						File.Delete(_dbPath);
						return null;
					}
				}
			} catch (Exception e) {
				_logger.Error(e, "Error reading {0}, deleting and ignoring.", _dbPath);
				return null;
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

		/// <summary>
		/// JSON serialization rules
		/// </summary>
		private readonly JsonSerializer _serializer = new JsonSerializer {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new SnakeCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};
	}
}
