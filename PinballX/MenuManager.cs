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

		private string rootFolder = (string)Properties.Settings.Default["PbxFolder"];
		public List<PinballXSystem> Systems { set; get; }

		public MenuManager()
		{
			Systems = new List<PinballXSystem>();
			if (rootFolder != null && rootFolder.Length > 0) {
				string pbxConfig = rootFolder + "\\Config\\PinballX.ini";
				if (File.Exists(pbxConfig)) {
					var parser = new FileIniDataParser();
					IniData data = parser.ReadFile(pbxConfig);

					Systems.Add(new PinballXSystem(Type.VP, data["VisualPinball"]));
					Systems.Add(new PinballXSystem(Type.FP, data["FuturePinball"]));
					for (int i = 0; i < 20; i++) {
						if (data["System_" + i] != null) {
							Systems.Add(new PinballXSystem(data["System_" + i]));
						}
					}
				}
			}
		}

		public Menu parseXml()
		{
			try {
				XmlSerializer serializer = new XmlSerializer(typeof(Menu));
				Stream reader = new FileStream("E:\\Pinball\\PinballX\\Databases\\Visual Pinball\\Visual Pinball.xml", FileMode.Open);
				return (Menu)serializer.Deserialize(reader);
			} catch (System.InvalidOperationException e) {
				Console.WriteLine("Error parsing XML: {0}", e.Message);
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
	}
}
