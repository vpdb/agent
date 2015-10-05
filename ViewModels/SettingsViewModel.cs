using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using ReactiveUI;

namespace VpdbAgent.ViewModels
{
	public class SettingsViewModel : ReactiveObject, IRoutableViewModel
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// screen
		public IScreen HostScreen { get; protected set; }
		public string UrlPathSegment => "settings";

		// deps
		private readonly ISettingsManager _settingsManager;

		// commands
		public ReactiveCommand<object> SaveSettings { get; protected set; }
		public ReactiveCommand<object> CloseSettings { get; protected set; } = ReactiveCommand.Create();

		private string _apiKey;
		private string _pbxFolder;
		private string _endpoint;
		private string _authUser;
		private string _authPass;

		public SettingsViewModel(IScreen screen, ISettingsManager settingsManager)
		{
			HostScreen = screen;
			_settingsManager = settingsManager;

			ApiKey = _settingsManager.ApiKey;
			AuthUser = _settingsManager.AuthUser;
			AuthPass = _settingsManager.AuthPass;
			Endpoint = _settingsManager.Endpoint;
			PbxFolder = _settingsManager.PbxFolder;

			SaveSettings = ReactiveCommand.Create();
			SaveSettings.Subscribe(_ => Save());

			CloseSettings.InvokeCommand(HostScreen.Router, r => r.NavigateBack);
		}

		private void Save()
		{
			_settingsManager.ApiKey = _apiKey;
			_settingsManager.AuthUser = _authUser;
			_settingsManager.AuthPass = _authPass;
			_settingsManager.Endpoint = _endpoint;
			_settingsManager.PbxFolder = _pbxFolder;

			var errors = _settingsManager.Validate();
			if (errors.Count == 0) {
				_settingsManager.Save();
				Logger.Info("Settings saved.");

			} else {

				// TODO properly display error
				foreach (var field in errors.Keys) {
					Logger.Error("Settings validation error for field {0}: {1}", field, errors[field]);
				}
			}
		}

		public string ApiKey
		{
			get { return this._apiKey; }
			set { this.RaiseAndSetIfChanged(ref this._apiKey, value); }
		}
		public string AuthUser
		{
			get { return this._authUser; }
			set { this.RaiseAndSetIfChanged(ref this._authUser, value); }
		}
		public string AuthPass
		{
			get { return this._authPass; }
			set { this.RaiseAndSetIfChanged(ref this._authPass, value); }
		}
		public string Endpoint
		{
			get { return this._endpoint; }
			set { this.RaiseAndSetIfChanged(ref this._endpoint, value); }
		}
		public string PbxFolder
		{
			get { return this._pbxFolder; }
			set { this.RaiseAndSetIfChanged(ref this._pbxFolder, value); }
		}
	}
}
