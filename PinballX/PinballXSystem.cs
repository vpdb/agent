using IniParser.Model;
using System;

namespace VpdbAgent.PinballX
{
	public class PinballXSystem
	{
		public string Name { get; set; }
		public bool Enabled { get; set; }
		public string WorkingPath { get; set; }
		public string TablePath { get; set; }
		public string Executable { get; set; }
		public string Parameters { get; set; }
		public Type Type { get; set; }

		public PinballXSystem(KeyDataCollection data)
		{
			setByData(data);

			string systemType = data["SystemType"];
			if ("0".Equals(systemType)) {
				Type = Type.CUSTOM;
			} else if ("1".Equals(systemType)) {
				Type = Type.VP;
			} else if ("2".Equals(systemType)) {
				Type = Type.FP;
			}
		}

		public PinballXSystem(Type type, KeyDataCollection data)
		{
			setByData(data);
			Type = type;
			switch (type) {
				case Type.VP:
					Name = "Visual Pinball";
					break;
				case Type.FP:
					Name = "Future Pinball";
					break;
				case Type.CUSTOM:
					Name = "Custom";
					break;
			}
		}

		private void setByData(KeyDataCollection data)
		{
			Name = data["Name"];
			Enabled = "true".Equals(data["Enabled"], StringComparison.InvariantCultureIgnoreCase);
			WorkingPath = data["WorkingPath"];
			TablePath = data["TablePath"];
			Executable = data["Executable"];
		}
	}


	public enum Type
	{
		VP, FP, CUSTOM
	}
}
