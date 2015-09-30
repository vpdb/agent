using IniParser;
using IniParser.Model;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ReactiveUI;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.PinballX
{
	public class MenuManager
	{
		private static MenuManager _instance;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Systems read from PinballX.ini.
		/// </summary>
		public ReactiveList<PinballXSystem> Systems { get; } = new ReactiveList<PinballXSystem>();

		// game change handlers
		public delegate void GameChangedHandler(PinballXSystem system);
		public event GameChangedHandler GamesChanged;

		private readonly FileWatcher _fileWatcher = FileWatcher.GetInstance();
		private readonly SettingsManager _settingsManager = SettingsManager.GetInstance();

		private string _dbPath;

		/// <summary>
		/// Private constructor
		/// </summary>
		/// <see cref="GetInstance"/>
		private MenuManager()
		{
		}

		public MenuManager Initialize()
		{
			if (!_settingsManager.IsInitialized()) {
				return this;
			}

			var iniPath = _settingsManager.PbxFolder + @"\Config\PinballX.ini";
			_dbPath = _settingsManager.PbxFolder + @"\Databases\";

			ParseSystems(iniPath);

			// setup file watchers
			_fileWatcher.SetupIni(iniPath).Subscribe(ParseSystems);
			_fileWatcher.SetupXml(_dbPath, Systems).Subscribe(ParseGames);

			return this;
		}

		/// <summary>
		/// Parses PinballX.ini and reads all systems from it.
		/// </summary>
		private void ParseSystems(string iniPath)
		{
			using (Systems.SuppressChangeNotifications()) {
				Logger.Info("Parsing systems from {0}", iniPath);
				Systems.Clear();
				if (File.Exists(iniPath)) {
					var parser = new FileIniDataParser();
					var data = parser.ReadFile(iniPath);
					Systems.Add(new PinballXSystem(VpdbAgent.Models.Platform.PlatformType.VP, data["VisualPinball"]));
					Systems.Add(new PinballXSystem(VpdbAgent.Models.Platform.PlatformType.FP, data["FuturePinball"]));
					for (var i = 0; i < 20; i++) {
						if (data["System_" + i] != null) {
							Systems.Add(new PinballXSystem(data["System_" + i]));
						}
					}
				} else {
					Logger.Error("PinballX.ini at {0} does not exist.", iniPath);
				}
				Logger.Info("Done, {0} systems parsed.", Systems.Count);
			}

			// parse games
			foreach (var system in Systems) {
				ParseGames(system.DatabasePath + @"\data.xml");
			}
		}

		private void ParseGames(string path)
		{
			Logger.Info("XML {0} changed, updating games.", path);

			var system = Systems.FirstOrDefault(s => s.DatabasePath.Equals(Path.GetDirectoryName(path)));

			if (system == null) {
				Logger.Warn("Unknown system at {0}, ignoring file change.", path);
				foreach (var s in Systems) {
					Logger.Warn("{0} != {1}", Path.GetDirectoryName(path), s.DatabasePath);
				}
				return;
			}
			var games = new List<Game>();
			var fileCount = 0;
			if (Directory.Exists(system.DatabasePath)) {
				foreach (var filePath in Directory.GetFiles(system.DatabasePath)
					.Where(filePath => "xml".Equals(filePath.Substring(filePath.Length - 3), StringComparison.InvariantCultureIgnoreCase))) {
					games.AddRange(ReadXml(filePath).Games);
					fileCount++;
				}
			}
			Logger.Debug("Parsed {0} games from {1} XML files at {2}.", games.Count, fileCount, system.DatabasePath);
			system.Games = games;

			// announce to subscribers
			GamesChanged?.Invoke(system);
		}

		private static Menu ReadXml(string filepath)
		{
			var menu = new Menu();
			Stream reader = null;
			try {
				var serializer = new XmlSerializer(typeof(Menu));
				reader = new FileStream(filepath, FileMode.Open);
				menu = serializer.Deserialize(reader) as Menu;

			} catch (Exception e) {
				Logger.Error(e, "Error parsing {0}: {1}", filepath, e.Message);

			} finally {
				reader?.Close();
			}
			return menu;
		}

		public static MenuManager GetInstance()
		{
			return _instance ?? (_instance = new MenuManager());
		}

		/*
		public List<Game> GetGames()
		{
			List<Game> games = new List<Game>();
			string xmlPath;
			foreach (PinballXSystem system in Systems) {
				xmlPath = dbPath + system.Name;
				if (system.Enabled) {
					games.AddRange(GetGames(xmlPath));
				}
			}
			return games;
		}

		public void saveXml(Menu menu)
		{
			XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
			XmlSerializer writer = new XmlSerializer(typeof(Menu));
			FileStream file = File.Create("C:\\Games\\PinballX\\Databases\\Visual Pinball\\Visual Pinball - backup.xml");
			ns.Add("", "");
			writer.Serialize(file, menu, ns);
			file.Close();
		}*/

	}
}
