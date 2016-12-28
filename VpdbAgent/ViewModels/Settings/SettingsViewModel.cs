using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NLog;
using ReactiveUI;
using VpdbAgent.Application;
using VpdbAgent.Models;

namespace VpdbAgent.ViewModels.Settings
{
	public class SettingsViewModel : ReactiveObject, IRoutableViewModel
	{
		// dependencies
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly ISettingsManager _settingsManager;
		private readonly IVersionManager _versionManager;
		private readonly IGameManager _gameManager;

		// setting props
		public string ApiKey { get { return _apiKey; } set { this.RaiseAndSetIfChanged(ref _apiKey, value); } }
		public string AuthUser { get { return _authUser; } set { this.RaiseAndSetIfChanged(ref _authUser, value); } }
		public string AuthPass { get { return _authPass; } set { this.RaiseAndSetIfChanged(ref _authPass, value); } }
		public string Endpoint { get { return _endpoint; } set { this.RaiseAndSetIfChanged(ref _endpoint, value); } }
		public string PbxFolder { get { return _pbxFolder; } set { this.RaiseAndSetIfChanged(ref _pbxFolder, value); } }
		public bool SyncStarred { get { return _syncStarred; } set { this.RaiseAndSetIfChanged(ref _syncStarred, value); } }
		public bool DownloadOnStartup { get { return _downloadOnStartup; } set { this.RaiseAndSetIfChanged(ref _downloadOnStartup, value); } }
		public bool PatchTableScripts { get { return _patchTableScripts; } set { this.RaiseAndSetIfChanged(ref _patchTableScripts, value); } }
		public bool MinimizeToTray { get { return _minimizeToTray; } set { this.RaiseAndSetIfChanged(ref _minimizeToTray, value); } }
		public bool StartWithWindows { get { return _startWithWindows; } set { this.RaiseAndSetIfChanged(ref _startWithWindows, value); } }
		public bool ReformatXml { get { return _reformatXml; } set { this.RaiseAndSetIfChanged(ref _reformatXml, value); } }
		public string XmlFileVP { get { return _xmlFileVP; } set { this.RaiseAndSetIfChanged(ref _xmlFileVP, value); } }
		public SettingsManager.Orientation DownloadOrientation { get { return _downloadOrientation; } set { this.RaiseAndSetIfChanged(ref _downloadOrientation, value); } }
		public SettingsManager.Orientation DownloadOrientationFallback { get { return _downloadOrientationFallback; } set { this.RaiseAndSetIfChanged(ref _downloadOrientationFallback, value); } }
		public SettingsManager.Lighting DownloadLighting { get { return _downloadLighting; } set { this.RaiseAndSetIfChanged(ref _downloadLighting, value); } }
		public SettingsManager.Lighting DownloadLightingFallback { get { return _downloadLightingFallback; } set { this.RaiseAndSetIfChanged(ref _downloadLightingFallback, value); } }

		// other props
		public string PbxFolderLabel => string.IsNullOrEmpty(_pbxFolder) ? "No folder set." : "Location:";
		public Dictionary<string, string> Errors { get { return _errors; } set { this.RaiseAndSetIfChanged(ref _errors, value); } }
		public bool ShowAdvancedOptions { get { return _showAdvancedOptions; } set { this.RaiseAndSetIfChanged(ref _showAdvancedOptions, value); } }
		public bool IsValidating => _isValidating.Value;
		public bool IsFirstRun => _isFirstRun.Value;
		public bool CanCancel => _canCancel.Value;
		public List<string> XmlFilesVP { get; } = new List<string>();
		public List<OrientationSetting> OrientationSettings { get; } = new List<OrientationSetting>();
		public List<OrientationSetting> OrientationFallbackSettings { get; } = new List<OrientationSetting>();
		public List<LightingSetting> LightingSettings { get; } = new List<LightingSetting>();
		public List<LightingSetting> LightingFallbackSettings { get; } = new List<LightingSetting>();

		// screen
		public IScreen HostScreen { get; protected set; }
		public string UrlPathSegment => "settings";

		// commands
		public ReactiveCommand<Unit, Dictionary<string, string>> SaveSettings { get; }
		public ReactiveCommand<Unit, Unit> ChooseFolder { get; }
		public CombinedReactiveCommand<Unit, Unit> CloseSettings { get; }
		public ReactiveCommand<Unit, Unit> ShowPatchTableInfo { get; }

		// privates
		private string _apiKey;
		private string _pbxFolder;
		private string _endpoint;
		private string _authUser;
		private string _authPass;
		private bool _syncStarred;
		private bool _downloadOnStartup;
		private bool _patchTableScripts;
		private bool _minimizeToTray;
		private bool _startWithWindows;
		private bool _reformatXml;
		private string _xmlFileVP;
		private SettingsManager.Orientation _downloadOrientation;
		private SettingsManager.Orientation _downloadOrientationFallback;
		private SettingsManager.Lighting _downloadLighting;
		private SettingsManager.Lighting _downloadLightingFallback;
		private bool _showAdvancedOptions;
		private Dictionary<string, string> _errors;
		private readonly ObservableAsPropertyHelper<bool> _isValidating;
		private readonly ObservableAsPropertyHelper<bool> _isFirstRun;
		private readonly ObservableAsPropertyHelper<bool> _canCancel;

		public SettingsViewModel(IScreen screen, ISettingsManager settingsManager, IVersionManager versionManager, IGameManager gameManager)
		{
			HostScreen = screen;
			_settingsManager = settingsManager;
			_versionManager = versionManager;
			_gameManager = gameManager;

			ApiKey = _settingsManager.Settings.ApiKey;
			AuthUser = _settingsManager.Settings.AuthUser;
			AuthPass = _settingsManager.Settings.AuthPass;
			Endpoint = _settingsManager.Settings.Endpoint;
			PbxFolder = _settingsManager.Settings.PbxFolder;
			SyncStarred = _settingsManager.Settings.SyncStarred;
			DownloadOnStartup = _settingsManager.Settings.DownloadOnStartup;
			PatchTableScripts = _settingsManager.Settings.PatchTableScripts;
			MinimizeToTray = _settingsManager.Settings.MinimizeToTray;
			ReformatXml = _settingsManager.Settings.ReformatXml;
			XmlFileVP = _settingsManager.Settings.XmlFile[Platform.PlatformType.VP];
			StartWithWindows = _settingsManager.Settings.StartWithWindows;
			DownloadOrientation = _settingsManager.Settings.DownloadOrientation;
			DownloadOrientationFallback = _settingsManager.Settings.DownloadOrientationFallback;
			DownloadLighting = _settingsManager.Settings.DownloadLighting;
			DownloadLightingFallback = _settingsManager.Settings.DownloadLightingFallback;

			SaveSettings = ReactiveCommand.CreateFromTask(_ => Save());
			SaveSettings.IsExecuting.ToProperty(this, vm => vm.IsValidating, out _isValidating);
			SaveSettings.ThrownExceptions.Subscribe(e =>
			{
				// todo either remove or treat correctly.
				Console.WriteLine("Exception while saving settings.");
			});

			ChooseFolder = ReactiveCommand.Create(OpenFolderDialog);
			CloseSettings = ReactiveCommand.CreateCombined(new[] { HostScreen.Router.NavigateBack });

			_settingsManager.WhenAnyValue(sm => sm.Settings.IsFirstRun).ToProperty(this, vm => vm.IsFirstRun, out _isFirstRun);
			_settingsManager.WhenAnyValue(sm => sm.CanCancel)
				.CombineLatest(screen.Router.NavigationStack.Changed, (canCancel, _) => canCancel || screen.Router.NavigationStack.Count > 1)
				.DistinctUntilChanged()
				.StartWith(true)
				.ToProperty(this, vm => vm.CanCancel, out _canCancel);
			
			OrientationSettings.Add(new OrientationSetting("Portrait", SettingsManager.Orientation.Portrait));
			OrientationSettings.Add(new OrientationSetting("Landscape", SettingsManager.Orientation.Landscape));
			OrientationSettings.Add(new OrientationSetting("Universal (VP10)", SettingsManager.Orientation.Universal));
			LightingSettings.Add(new LightingSetting("Day", SettingsManager.Lighting.Day));
			LightingSettings.Add(new LightingSetting("Night", SettingsManager.Lighting.Night));
			LightingSettings.Add(new LightingSetting("Universal (VP10)", SettingsManager.Lighting.Universal));
			XmlFilesVP.Add("Visual Pinball");
			XmlFilesVP.Add("Vpdb");
			OrientationFallbackSettings.Add(new OrientationSetting("Same *", SettingsManager.Orientation.Same));
			OrientationFallbackSettings.Add(new OrientationSetting("Portrait", SettingsManager.Orientation.Portrait));
			OrientationFallbackSettings.Add(new OrientationSetting("Landscape", SettingsManager.Orientation.Landscape));
			OrientationFallbackSettings.Add(new OrientationSetting("Any", SettingsManager.Orientation.Any));
			LightingFallbackSettings.Add(new LightingSetting("Same *", SettingsManager.Lighting.Same));
			LightingFallbackSettings.Add(new LightingSetting("Day", SettingsManager.Lighting.Day));
			LightingFallbackSettings.Add(new LightingSetting("Night", SettingsManager.Lighting.Night));
			LightingFallbackSettings.Add(new LightingSetting("Any", SettingsManager.Lighting.Any));
		}

		public SettingsViewModel(IScreen screen, ISettingsManager settingsManager, IVersionManager versionManager, IGameManager gameManager, Dictionary<string, string> errors) : this(screen, settingsManager, versionManager, gameManager)
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
			if (result == DialogResult.OK) {
				PbxFolder = dialog.SelectedPath;
				Logger.Info("PinballX folder set to {0}.", PbxFolder);
			}
		}

		private async Task<Dictionary<string, string>> Save()
		{
			var settings = _settingsManager.Settings.Copy();

			settings.ApiKey = _apiKey;
			settings.AuthUser = _authUser;
			settings.AuthPass = _authPass;
			settings.Endpoint = _endpoint;
			settings.PbxFolder = _pbxFolder;
			settings.SyncStarred = _syncStarred;
			settings.DownloadOnStartup = _downloadOnStartup;
			settings.PatchTableScripts = _patchTableScripts;
			settings.MinimizeToTray = _minimizeToTray;
			settings.ReformatXml = _reformatXml;
			settings.XmlFile = new Dictionary<Platform.PlatformType, string> {{ Platform.PlatformType.VP, _xmlFileVP }};
			settings.StartWithWindows = _startWithWindows;
			settings.DownloadOrientation = _downloadOrientation;
			settings.DownloadOrientationFallback = _downloadOrientationFallback;
			settings.DownloadLighting = _downloadLighting;
			settings.DownloadLightingFallback = _downloadLightingFallback;

			var errors = await _settingsManager.Validate(settings);

			if (settings.IsValidated) {
				_settingsManager.Save(settings).SubscribeOn(Scheduler.CurrentThread).ObserveOn(Scheduler.CurrentThread).Subscribe(_ =>
				{
					Logger.Info("Settings saved.");
					System.Windows.Application.Current.Dispatcher.Invoke(delegate {
						if (HostScreen.Router.NavigationStack.Count == 1) {
							_gameManager.Initialize();
							HostScreen.Router.NavigateAndReset.Execute(new MainViewModel(HostScreen, _settingsManager, _versionManager));

						} else {
							HostScreen.Router.NavigateBack.Execute();
						}
					});
				});
			} else {
				Errors = errors;
				if (errors.ContainsKey("Auth")) {
					ShowAdvancedOptions = true;
				}
			}
			return errors;
		}

		public class OrientationSetting
		{
			public string Label { get; }
			public SettingsManager.Orientation Orientation { get; }
			public OrientationSetting(string label, SettingsManager.Orientation orientation)
			{
				Label = label;
				Orientation = orientation;
			}
		}

		public class LightingSetting
		{
			public string Label { get; }
			public SettingsManager.Lighting Lighting { get; }
			public LightingSetting(string label, SettingsManager.Lighting lighting)
			{
				Label = label;
				Lighting = lighting;
			}
		}
	}
}
