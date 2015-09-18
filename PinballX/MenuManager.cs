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
		public Menu parseXml()
		{
			try
			{
				XmlSerializer serializer = new XmlSerializer(typeof(Menu));
				Stream reader = new FileStream("E:\\Pinball\\PinballX\\Databases\\Visual Pinball\\Visual Pinball.xml", FileMode.Open);
				return (Menu)serializer.Deserialize(reader);
			}
			catch (System.InvalidOperationException e)
			{
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
