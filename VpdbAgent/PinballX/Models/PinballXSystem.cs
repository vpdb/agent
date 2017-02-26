using IniParser.Model;
using System;
using System.IO;
using ReactiveUI;
using VpdbAgent.Application;
using VpdbAgent.Data.Objects;

namespace VpdbAgent.PinballX.Models
{
	/// <summary>
	/// A "system" as read PinballX.
	/// 
	/// This comes live from PinballX.ini and resides only in memory. It's 
	/// updated when PinballX.ini changes.
	/// </summary>
	public class PinballXSystem : ReactiveObject
	{
		// deps
		private readonly ISettingsManager _settingsManager;

		// from pinballx.ini
		public string Name { get; set; }
		public bool Enabled { get { return _enabled; } set { this.RaiseAndSetIfChanged(ref _enabled, value); } }
		public string WorkingPath { get; set; }
		public string TablePath { get { return _tablePath; } set { this.RaiseAndSetIfChanged(ref _tablePath, value); } }
		public string Executable { get { return _executable; } set { this.RaiseAndSetIfChanged(ref _executable, value); } }
		public string Parameters { get; set; }
		public PlatformType Type { get { return _type; } set { this.RaiseAndSetIfChanged(ref _type, value); } }

		// watched props
		private bool _enabled;
		private string _tablePath;
		private string _executable;
		private PlatformType _type;

		// convenient props
		public string DatabasePath { get; set; }
		public string MediaPath { get; set; }

		// games
		public ReactiveList<PinballXGame> Games { get; } = new ReactiveList<PinballXGame>();

		public PinballXSystem(ISettingsManager settingsManager)
		{
			_settingsManager = settingsManager;
		}

		public PinballXSystem(KeyDataCollection data, ISettingsManager settingsManager) : this(settingsManager)
		{
			var systemType = data["SystemType"];
			if ("0".Equals(systemType)) {
				Type = PlatformType.Custom;
			} else if ("1".Equals(systemType)) {
				Type = PlatformType.VP;
			} else if ("2".Equals(systemType)) {
				Type = PlatformType.FP;
			}
			Name = data["Name"];

			SetByData(data);
		}

		public PinballXSystem(PlatformType type, KeyDataCollection data, ISettingsManager settingsManager) : this(settingsManager)
		{
			Type = type;
			switch (type) {
				case PlatformType.VP:
					Name = "Visual Pinball";
					break;
				case PlatformType.FP:
					Name = "Future Pinball";
					break;
				case PlatformType.Custom:
					Name = "Custom";
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, null);
			}
			SetByData(data);
		}

		/// <summary>
		/// Copies data over from another system
		/// </summary>
		/// <param name="system">New system</param>
		/// <returns></returns>
		public PinballXSystem Update(PinballXSystem system)
		{
			Enabled = system.Enabled;
			WorkingPath = system.WorkingPath;
			TablePath = system.TablePath;
			Executable = system.Executable;
			Parameters = system.Parameters;
			Type = system.Type;
			return this;
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

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) {
				return false;
			}
			if (ReferenceEquals(this, obj)) {
				return true;
			}
			var system = obj as PinballXSystem;
			if (system == null) {
				return false;
			}
			return 
				Name == system.Name &&
				Enabled == system.Enabled &&
				WorkingPath == system.WorkingPath &&
				TablePath == system.TablePath &&
				Executable == system.Executable &&
				Parameters == system.Parameters &&
				Type == system.Type;
		}

		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return Name.GetHashCode();
		}

		public override string ToString()
		{
			return $"[System] {Name} ({Games.Count})";
		}
	}
}
