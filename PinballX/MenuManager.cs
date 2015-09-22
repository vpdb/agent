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
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.PinballX
{
	public class MenuManager
	{
		private static MenuManager INSTANCE;
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		public List<PinballXSystem> Systems { set; get; } = new List<PinballXSystem>();

		// system change handlers
		public delegate void SystemsChangedHandler(List<PinballXSystem> systems);
		public event SystemsChangedHandler SystemsChanged;

		// game change handlers
		public delegate void GameChangedHandler(PinballXSystem system);
		public event GameChangedHandler GamesChanged;

		private readonly FileWatcher fileWatcher = FileWatcher.GetInstance();
		private readonly SettingsManager settingsManager = SettingsManager.GetInstance();

		private string iniPath;
		private string dbPath;

		/// <summary>
		/// Private constructor
		/// </summary>
		/// <see cref="GetInstance"/>
		private MenuManager()
		{
		}

		public MenuManager Initialize()
		{
			if (settingsManager.IsInitialized()) {

				iniPath = settingsManager.PbxFolder + @"\Config\PinballX.ini";
				dbPath = settingsManager.PbxFolder + @"\Databases\";

				parseSystems();
				
				// setup file watchers
				fileWatcher.SetupIni(iniPath);
				fileWatcher.SetupXml(dbPath, Systems);
				fileWatcher.IniChanged += new FileWatcher.IniChangedHandler(parseSystems);
				fileWatcher.XmlChanged += new FileWatcher.XmlChangedHandler(parseGames);
			}
			return this;
		}

		/// <summary>
		/// Parses PinballX.ini and reads all systems from it.
		/// </summary>
		private void parseSystems()
		{
			logger.Info("Parsing systems from PinballX.ini");
			Systems.Clear();
			if (File.Exists(iniPath)) {
				var parser = new FileIniDataParser();
				IniData data = parser.ReadFile(iniPath);
				Systems.Add(new PinballXSystem(VpdbAgent.Models.Platform.PlatformType.VP, data["VisualPinball"]));
				Systems.Add(new PinballXSystem(VpdbAgent.Models.Platform.PlatformType.FP, data["FuturePinball"]));
				for (int i = 0; i < 20; i++) {
					if (data["System_" + i] != null) {
						Systems.Add(new PinballXSystem(data["System_" + i]));
					}
				}
			} else {
				logger.Error("PinballX.ini at {0} does not exist.", iniPath);
			}
			logger.Info("Done, {0} systems parsed.", Systems.Count);

			// announce to subscribers
			if (SystemsChanged != null) {
				SystemsChanged(Systems);
			}

			// parse games
			foreach (PinballXSystem system in Systems) {
				parseGames(system.DatabasePath + @"\data.xml", WatcherChangeTypes.Created);
			}
		}

		private void parseGames(string path, WatcherChangeTypes type)
		{
			PinballXSystem system = Systems.Where(s => { return s.DatabasePath.Equals(Path.GetDirectoryName(path)); }).FirstOrDefault();

			if (system == null) {
				logger.Warn("Unknown system at {0}, ignoring file change.", path);
				foreach(PinballXSystem s in Systems) {
					logger.Warn("{0} != {1}", Path.GetDirectoryName(path), s.DatabasePath);
				}
				return;
			}
			List<Game> games = new List<Game>();
			int fileCount = 0;
			if (Directory.Exists(system.DatabasePath)) {
				foreach (string filePath in Directory.GetFiles(system.DatabasePath)) {
					if ("xml".Equals(filePath.Substring(filePath.Length - 3), StringComparison.InvariantCultureIgnoreCase)) {
						games.AddRange(readXml(filePath).Games);
						fileCount++;
					}
				}
			}
			logger.Debug("Parsed {0} games from {1} XML files at {2}.", games.Count, fileCount, system.DatabasePath);
			system.Games = games;

			// announce to subscribers
			if (GamesChanged != null) {
				GamesChanged(system);
			}
		}

		private Menu readXml(string filepath)
		{
			Menu menu = new Menu();
			Stream reader = null;
			try {
				XmlSerializer serializer = new XmlSerializer(typeof(Menu));
				reader = new FileStream(filepath, FileMode.Open);
				menu = serializer.Deserialize(reader) as Menu;

			} catch (Exception e) {
				logger.Error(e, "Error parsing {0}: {1}", filepath, e.Message);

			} finally {
				if (reader != null) {
					reader.Close();
				}
			}
			return menu;
		}

		public static MenuManager GetInstance()
		{
			if (INSTANCE == null) {
				INSTANCE = new MenuManager();
			}
			return INSTANCE;
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
