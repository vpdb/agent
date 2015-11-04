using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

		private readonly Subject<string> _pbxFolderChanged = new Subject<string>();
		private readonly Subject<Unit> _apiChanged = new Subject<Unit>();

		public IObservable<string> PbxFolderChanged => _pbxFolderChanged;
		public IObservable<Unit> ApiChanged => _apiChanged;
		public IObservable<EventPattern<PropertyChangedEventArgs>> Changed { get; }

		public SettingsManager()
		{
			ApiKey = (string)Properties.Settings.Default["ApiKey"];
			AuthUser = (string)Properties.Settings.Default["AuthUser"];
			AuthPass = (string)Properties.Settings.Default["AuthPass"];
			Endpoint = (string)Properties.Settings.Default["Endpoint"];
			PbxFolder = (string)Properties.Settings.Default["PbxFolder"];

			Changed = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
				handler => handler.Invoke,
				ev => Properties.Settings.Default.PropertyChanged += ev,
				ev => Properties.Settings.Default.PropertyChanged -= ev);

			Changed
				.Where(args => args.EventArgs.PropertyName.Equals("PbxFolder"))
				.Select(args => PbxFolder)
				.Subscribe(_pbxFolderChanged);

			Changed
				.Where(args => new[] { "ApiKey", "AuthUser", "AuthPass", "Endpoint" }.Contains(args.EventArgs.PropertyName))
				.Select(args => Unit.Default)
				.Subscribe(_apiChanged);
		}

		public bool IsInitialized()
		{
			return !string.IsNullOrEmpty(PbxFolder);
		}

		public Dictionary<string, string> Validate()
		{
			var errors = new Dictionary<string, string>();
			if (string.IsNullOrEmpty(PbxFolder)) {
				errors.Add("PbxFolder", "The folder where PinballX is installed must be set.");
			} else if (!Directory.Exists(PbxFolder) || !Directory.Exists(PbxFolder + @"\Config")) {
				errors.Add("PbxFolder", "The folder \"" + PbxFolder + "\" is not a valid PinballX folder.");
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