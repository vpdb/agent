using IniParser;
using IniParser.Model;
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
		private static readonly string rootFolder = (string)Properties.Settings.Default["PbxFolder"];
		private static readonly string iniPath = rootFolder + @"\Config\PinballX.ini";

		public List<PinballXSystem> Systems { set; get; }

		private MenuManager()
		{
			parseIni();

			FileWatcher fileWatcher = FileWatcher.GetInstance();
			fileWatcher.SetupIni(iniPath);
			fileWatcher.IniChanged += new FileWatcher.IniChangedHandler(parseIni);
		}

		/// <summary>
		/// Parses PinballX.ini and reads all systems from it.
		/// </summary>
		private void parseIni()
		{
			Console.WriteLine("Parsing systems from PinballX.ini");
			Systems = new List<PinballXSystem>();
			if (rootFolder != null && rootFolder.Length > 0) {
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
			}
			Console.WriteLine("Done, {0} systems parsed.", Systems.Count);
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
				xmlPath = rootFolder + @"\Databases\" + system.Name;
				if (system.Enabled) {
					games.AddRange(GetGames(xmlPath));
				}
			}
			return games;
		}

		private Menu parseXml(string filepath)
		{
			try {
				XmlSerializer serializer = new XmlSerializer(typeof(Menu));
				Stream reader = new FileStream(filepath, FileMode.Open);
				return serializer.Deserialize(reader) as Menu;

			} catch (System.InvalidOperationException e) {
				Console.WriteLine("Error parsing {0}: {1}", filepath, e.Message);
			}
			return new Menu();
		}

		public void saveXml(Menu menu)
		{
			XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
			XmlSerializer writer = new XmlSerializer(typeof(Menu));
			FileStream file = File.Create("C:\\Games\\PinballX\\Databases\\Visual Pinball\\Visual Pinball - backup.xml");
			ns.Add("", "");
			writer.Serialize(file, menu, ns);
			file.Close();
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
