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

		public delegate void SystemsChangedHandler(List<PinballXSystem> systems);
		public event SystemsChangedHandler SystemsChanged;

		private readonly SettingsManager settingsManager = SettingsManager.GetInstance();
		private string iniPath;

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
				parseIni();

				FileWatcher fileWatcher = FileWatcher.GetInstance();
				fileWatcher.SetupIni(iniPath);
				fileWatcher.IniChanged += new FileWatcher.IniChangedHandler(parseIni);
			}
			return this;
		}

		public List<Game> GetGames(string path)
		{
			List<Game> games = new List<Game>();
			if (Directory.Exists(path)) {
				foreach (string filePath in Directory.GetFiles(path)) {
					if ("xml".Equals(filePath.Substring(filePath.Length - 3), StringComparison.InvariantCultureIgnoreCase)) {
						games.AddRange(parseXml(filePath).Games);
					}
				}
			}
			return games;
		}

		public List<Game> GetGames()
		{
			List<Game> games = new List<Game>();
			string xmlPath;
			foreach (PinballXSystem system in Systems) {
				xmlPath = settingsManager.PbxFolder + @"\Databases\" + system.Name;
				if (system.Enabled) {
					games.AddRange(GetGames(xmlPath));
				}
			}
			return games;
		}

/*		public void saveXml(Menu menu)
		{
			XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
			XmlSerializer writer = new XmlSerializer(typeof(Menu));
			FileStream file = File.Create("C:\\Games\\PinballX\\Databases\\Visual Pinball\\Visual Pinball - backup.xml");
			ns.Add("", "");
			writer.Serialize(file, menu, ns);
			file.Close();
		}*/

		/// <summary>
		/// Parses PinballX.ini and reads all systems from it.
		/// </summary>
		private void parseIni()
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
			}
			logger.Info("Done, {0} systems parsed.", Systems.Count);
			SystemsChanged(Systems);
		}

		private Menu parseXml(string filepath)
		{
			Menu menu = new Menu();
			Stream reader = null;
			try {
				XmlSerializer serializer = new XmlSerializer(typeof(Menu));
				reader = new FileStream(filepath, FileMode.Open);
				menu = serializer.Deserialize(reader) as Menu;

			} catch (System.InvalidOperationException e) {
				logger.Error(e, "Error parsing {0}: {1}", filepath, e.Message);
				
			} finally {
				if (reader != null) {
					reader.Close();
				}
			}
			return new Menu();
		}

		public static MenuManager GetInstance()
		{
			if (INSTANCE == null) {
				INSTANCE = new MenuManager();
			}
			return INSTANCE;
		}

	}
}
