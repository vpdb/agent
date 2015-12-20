using IniParser.Model;
using System;
using System.IO;
using ReactiveUI;
using VpdbAgent.Models;
using VpdbAgent.Application;

namespace VpdbAgent.PinballX.Models
{
	/// <summary>
	/// A "system" as read PinballX.
	/// 
	/// This comes live from PinballX.ini and resides only in memory. It's 
	/// updated when PinballX.ini changes.
	/// </summary>
	public class PinballXSystem
	{
		// deps
		private readonly ISettingsManager _settingsManager;

		// from pinballx.ini
		public string Name { get; set; }
		public bool Enabled { get; set; }
		public string WorkingPath { get; set; }
		public string TablePath { get; set; }
		public string Executable { get; set; }
		public string Parameters { get; set; }
		public Platform.PlatformType Type { get; set; }

		// convenient props
		public string DatabasePath { get; set; }
		public string MediaPath { get; set; }

		// data props
		public ReactiveList<PinballXGame> Games { get; } = new ReactiveList<PinballXGame>();

		public PinballXSystem(ISettingsManager settingsManager)
		{
			_settingsManager = settingsManager;
		}

		public PinballXSystem(KeyDataCollection data, ISettingsManager settingsManager) : this(settingsManager)
		{
			var systemType = data["SystemType"];
			if ("0".Equals(systemType)) {
				Type = Platform.PlatformType.Custom;
			} else if ("1".Equals(systemType)) {
				Type = Platform.PlatformType.VP;
			} else if ("2".Equals(systemType)) {
				Type = Platform.PlatformType.FP;
			}
			Name = data["Name"];

			SetByData(data);
		}

		public PinballXSystem(Platform.PlatformType type, KeyDataCollection data, ISettingsManager settingsManager) : this(settingsManager)
		{
			Type = type;
			switch (type) {
				case Platform.PlatformType.VP:
					Name = "Visual Pinball";
					break;
				case Platform.PlatformType.FP:
					Name = "Future Pinball";
					break;
				case Platform.PlatformType.Custom:
					Name = "Custom";
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, null);
			}
			SetByData(data);
		}

		/// <summary>
		/// Copies system data from PinballX.ini to our object.
		/// TODO validate (e.g. no TablePath should result at least in an error (now it crashes))
		/// </summary>
		/// <param name="data">Parsed data</param>
		private void SetByData(KeyDataCollection data)
		{
			Enabled = "true".Equals(data["Enabled"], StringComparison.InvariantCultureIgnoreCase);
			WorkingPath = data["WorkingPath"];
			TablePath = data["TablePath"];
			Executable = data["Executable"];
			Parameters = data["Parameters"];

			DatabasePath = Path.Combine(_settingsManager.Settings.PbxFolder, "Databases", Name);
			MediaPath = Path.Combine(_settingsManager.Settings.PbxFolder, "Media", Name);
		}

		public override string ToString()
		{
			return $"[System] {Name} ({Games.Count})";
		}
	}
}
