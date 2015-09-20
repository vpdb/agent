using IniParser.Model;
using System;
using VpdbAgent.Models;

namespace VpdbAgent.PinballX
{
	public class PinballXSystem
	{

		private static readonly string rootFolder = (string)Properties.Settings.Default["PbxFolder"];

		public string Name { get; set; }
		public bool Enabled { get; set; }
		public string WorkingPath { get; set; }
		public string TablePath { get; set; }
		public string Executable { get; set; }
		public string Parameters { get; set; }
		public Platform.PlatformType Type { get; set; }

		public string DatabasePath { get; set; }
		public string MediaPath { get; set; }

		public PinballXSystem(KeyDataCollection data)
		{
			string systemType = data["SystemType"];
			if ("0".Equals(systemType)) {
				Type = Platform.PlatformType.CUSTOM;
			} else if ("1".Equals(systemType)) {
				Type = Platform.PlatformType.VP;
			} else if ("2".Equals(systemType)) {
				Type = Platform.PlatformType.FP;
			}
			Name = data["Name"];

			setByData(data);
		}

		public PinballXSystem(Platform.PlatformType type, KeyDataCollection data)
		{
			Type = type;
			switch (type) {
				case Platform.PlatformType.VP:
					Name = "Visual Pinball";
					break;
				case Platform.PlatformType.FP:
					Name = "Future Pinball";
					break;
				case Platform.PlatformType.CUSTOM:
					Name = "Custom";
					break;
			}
			setByData(data);
		}

		private void setByData(KeyDataCollection data)
		{
			
			Enabled = "true".Equals(data["Enabled"], StringComparison.InvariantCultureIgnoreCase);
			WorkingPath = data["WorkingPath"];
			TablePath = data["TablePath"];
			Executable = data["Executable"];

			DatabasePath = rootFolder + @"\Databases\" + Name;
			MediaPath = rootFolder + @"\Media\" + Name;
		}
	}
}
