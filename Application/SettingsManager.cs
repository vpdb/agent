using System.Collections.Generic;
using System.IO;
using NLog;

namespace VpdbAgent.Application
{
	public class SettingsManager : ISettingsManager
	{
		public string ApiKey { get; set; }
		public string AuthUser { get; set; }
		public string AuthPass { get; set; }
		public string Endpoint { get; set; }
		public string PbxFolder { get; set; }

		public SettingsManager()
		{
			ApiKey = (string)Properties.Settings.Default["ApiKey"];
			AuthUser = (string)Properties.Settings.Default["AuthUser"];
			AuthPass = (string)Properties.Settings.Default["AuthPass"];
			Endpoint = (string)Properties.Settings.Default["Endpoint"];
			PbxFolder = (string)Properties.Settings.Default["PbxFolder"];
		}

		public bool IsInitialized()
		{
			return !string.IsNullOrEmpty(PbxFolder);
		}

		public Dictionary<string, string> Validate()
		{
			var errors = new Dictionary<string, string>();
			if (!Directory.Exists(PbxFolder) || !Directory.Exists(PbxFolder + @"\Config")) {
				errors.Add("PbxFolder", "Folder \"" + PbxFolder + "\" is not a valid PinballX folder.");
			}
			return errors;
		}

		public SettingsManager Save()
		{
			Properties.Settings.Default["ApiKey"] = ApiKey;
			Properties.Settings.Default["AuthUser"] = AuthUser;
			Properties.Settings.Default["AuthPass"] = AuthPass;
			Properties.Settings.Default["Endpoint"] = Endpoint;
			Properties.Settings.Default["PbxFolder"] = PbxFolder;
			Properties.Settings.Default.Save();
			return this;
		}
		
	}

	public interface ISettingsManager
	{
		string ApiKey { get; set; }
		string AuthUser { get; set; }
		string AuthPass { get; set; }
		string Endpoint { get; set; }
		string PbxFolder { get; set; }

		bool IsInitialized();
		Dictionary<string, string> Validate();
		SettingsManager Save();
	}
}