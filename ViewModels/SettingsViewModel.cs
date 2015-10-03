using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace VpdbAgent.ViewModels
{
	public class SettingsViewModel : ReactiveObject, IRoutableViewModel
	{
		// screen
		public IScreen HostScreen { get; protected set; }
		public string UrlPathSegment => "settings";

		// deps
		private readonly SettingsManager _settingsManager = SettingsManager.GetInstance();

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

			this.SaveCommand = ReactiveCommand.Create(
				 this.WhenAny(
					x => x.ApiKey,
					x => x.Endpoint,
					x => x.PbxFolder,
					(apiKey, endPoint, pbxFolder) =>
						!string.IsNullOrWhiteSpace(apiKey.Value) &&
						!string.IsNullOrWhiteSpace(endPoint.Value) &&
						Directory.Exists(pbxFolder.Value)));
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

		public ReactiveCommand<object> SaveCommand
		{
			get;
			private set;
		}
	}
}
