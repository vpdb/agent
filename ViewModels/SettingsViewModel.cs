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
		private readonly SettingsManager _settingsManager = SettingsManager.GetInstance();

		// commands
		public ReactiveCommand<object> SaveSettings { get; protected set; }
		public ReactiveCommand<object> CloseSettings { get; protected set; }

		private string _apiKey;
		private string _pbxFolder;
		private string _apiEndpoint;
		private string _authUser;
		private string _authPass;

		public SettingsViewModel(IScreen screen)
		{
			HostScreen = screen;

			_apiKey = _settingsManager.ApiKey;
			_authUser = _settingsManager.AuthUser;
			_authPass = _settingsManager.AuthPass;
			_apiEndpoint = _settingsManager.Endpoint;
			_pbxFolder = _settingsManager.PbxFolder;


			SaveSettings = ReactiveCommand.Create();
			SaveSettings.Subscribe(_ => Save());

			CloseSettings = ReactiveCommand.Create();
			CloseSettings.Subscribe(_ => Close());
		}

		private void Save()
		{
			_settingsManager.ApiKey = _apiKey;
			_settingsManager.AuthUser = _authUser;
			_settingsManager.AuthPass = _authPass;
			_settingsManager.Endpoint = _apiEndpoint;
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

		private void Close()
		{
			HostScreen.Router.NavigateBack.Execute(null);
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
			get { return this._apiEndpoint; }
			set { this.RaiseAndSetIfChanged(ref this._apiEndpoint, value); }
		}
		public string PbxFolder
		{
			get { return this._pbxFolder; }
			set { this.RaiseAndSetIfChanged(ref this._pbxFolder, value); }
		}
	}
}
