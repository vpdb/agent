using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NLog;
using ReactiveUI;
using Refit;
using Squirrel;
using VpdbAgent.Application;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.ViewModels.Settings
{
	public class SettingsViewModel : ReactiveObject, IRoutableViewModel
	{
		// dependencies
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly ISettingsManager _settingsManager;
		private readonly IVersionManager _versionManager;

		// setting props
		public string ApiKey { get { return _apiKey; } set { this.RaiseAndSetIfChanged(ref _apiKey, value); } }
		public string AuthUser { get { return _authUser; } set { this.RaiseAndSetIfChanged(ref _authUser, value); } }
		public string AuthPass { get { return _authPass; } set { this.RaiseAndSetIfChanged(ref _authPass, value); } }
		public string Endpoint { get { return _endpoint; } set { this.RaiseAndSetIfChanged(ref _endpoint, value); } }
		public string PbxFolder { get { return _pbxFolder; } set { this.RaiseAndSetIfChanged(ref _pbxFolder, value); } }

		// other props
		public string PbxFolderLabel => string.IsNullOrEmpty(_pbxFolder) ? "No folder set." : "Location:";
		public Dictionary<string, string> Errors { get { return _errors; } set { this.RaiseAndSetIfChanged(ref _errors, value); } }
		public bool ShowAdvancedOptions { get { return _showAdvancedOptions; } set { this.RaiseAndSetIfChanged(ref _showAdvancedOptions, value); } }
		public bool IsValidating => _isValidating.Value;
		public bool IsFirstRun => _isFirstRun.Value;
		public bool CanCancel => _canCancel.Value;

		// screen
		public IScreen HostScreen { get; protected set; }
		public string UrlPathSegment => "settings";

		// commands
		public ReactiveCommand<Dictionary<string, string>> SaveSettings { get; }
		public ReactiveCommand<object> ChooseFolder { get; } = ReactiveCommand.Create();
		public ReactiveCommand<object> CloseSettings { get; } = ReactiveCommand.Create();

		// privates
		private string _apiKey;
		private string _pbxFolder;
		private string _endpoint;
		private string _authUser;
		private string _authPass;
		private bool _showAdvancedOptions;
		private Dictionary<string, string> _errors;
		private readonly ObservableAsPropertyHelper<bool> _isValidating;
		private readonly ObservableAsPropertyHelper<bool> _isFirstRun;
		private readonly ObservableAsPropertyHelper<bool> _canCancel;

		public SettingsViewModel(IScreen screen, ISettingsManager settingsManager, IVersionManager versionManager)
		{
			HostScreen = screen;
			_settingsManager = settingsManager;
			_versionManager = versionManager;

			ApiKey = _settingsManager.ApiKey;
			AuthUser = _settingsManager.AuthUser;
			AuthPass = _settingsManager.AuthPass;
			Endpoint = _settingsManager.Endpoint;
			PbxFolder = _settingsManager.PbxFolder;

			SaveSettings = ReactiveCommand.CreateAsyncTask(_ => Save());
			SaveSettings.IsExecuting.ToProperty(this, vm => vm.IsValidating, out _isValidating);
			SaveSettings.ThrownExceptions.Subscribe(e =>
			{
				Console.WriteLine("Exception while saving settings.");
			});

			ChooseFolder.Subscribe(_ => OpenFolderDialog());
			CloseSettings.InvokeCommand(HostScreen.Router, r => r.NavigateBack);

			_settingsManager.WhenAnyValue(sm => sm.IsFirstRun).ToProperty(this, vm => vm.IsFirstRun, out _isFirstRun);
			_settingsManager.WhenAnyValue(sm => sm.CanCancel)
				.CombineLatest(screen.Router.NavigationStack.Changed, (canCancel, _) => canCancel || screen.Router.NavigationStack.Count > 1)
				.DistinctUntilChanged()
				.StartWith(true)
				.ToProperty(this, vm => vm.CanCancel, out _canCancel);

		}

		public SettingsViewModel(IScreen screen, ISettingsManager settingsManager, IVersionManager versionManager, Dictionary<string, string> errors) : this(screen, settingsManager, versionManager)
		{
			Errors = errors;
		}

		private void OpenFolderDialog()
		{
			var dialog = new FolderBrowserDialog {
				ShowNewFolderButton = false
			};

			if (!string.IsNullOrWhiteSpace(PbxFolder)) {
				dialog.SelectedPath = PbxFolder;
			}
			var result = dialog.ShowDialog();
			PbxFolder = result == DialogResult.OK ? dialog.SelectedPath : string.Empty;
			Logger.Info("PinballX folder set to {0}.", PbxFolder);
		}

		private async Task<Dictionary<string, string>> Save()
		{
			_settingsManager.ApiKey = _apiKey;
			_settingsManager.AuthUser = _authUser;
			_settingsManager.AuthPass = _authPass;
			_settingsManager.Endpoint = _endpoint;
			_settingsManager.PbxFolder = _pbxFolder;

			var errors = await _settingsManager.Validate();

			if (errors.Count == 0) {

				var firstRun = _settingsManager.IsFirstRun;
				_settingsManager.Save();
				Logger.Info("Settings saved.");

				if (HostScreen.Router.NavigationStack.Count == 1) {
					HostScreen.Router.NavigateAndReset.Execute(new MainViewModel(HostScreen, _settingsManager, _versionManager));
				} else {
					HostScreen.Router.NavigateBack.Execute(null);
				}

			} else {
				Errors = errors;
				if (errors.ContainsKey("Auth")) {
					ShowAdvancedOptions = true;
				}
			}
			return errors;
		}
	}
}
