using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Akavache;
using ReactiveUI;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Application
{
	public class Settings : ReactiveObject
	{
		/// <summary>
		/// VPDB's API key
		/// </summary>
		public string ApiKey { get { return _apiKey; } set { this.RaiseAndSetIfChanged(ref _apiKey, value); } }

		/// <summary>
		/// If HTTP Basic authentication is enabled on VPDB, this is the user name.
		/// </summary>
		public string AuthUser { get { return _authUser; } set { this.RaiseAndSetIfChanged(ref _authUser, value); } }

		/// <summary>
		/// If HTTP Basic authentication is enabled on VPDB, this is the password.
		/// </summary>
		public string AuthPass { get { return _authPass; } set { this.RaiseAndSetIfChanged(ref _authPass, value); } }

		/// <summary>
		/// The endpoint of the VPDB API.
		/// </summary>
		public string Endpoint { get { return _endpoint; } set { this.RaiseAndSetIfChanged(ref _endpoint, value); } }

		/// <summary>
		/// The local folder where the user installed PinballX
		/// </summary>
		public string PbxFolder { get { return _pbxFolder; } set { this.RaiseAndSetIfChanged(ref _pbxFolder, value); } }

		/// <summary>
		/// If true, starring a release on vpdb.io will make it synced here.
		/// </summary>
		public bool SyncStarred { get { return _syncStarred; } set { this.RaiseAndSetIfChanged(ref _syncStarred, value); } }

		/// <summary>
		/// If true, the app window mininized to the system tray instead of the application toolbar.
		/// </summary>
		public bool MinimizeToTray { get { return _minimizeToTray; } set { this.RaiseAndSetIfChanged(ref _minimizeToTray, value); } }

		/// <summary>
		/// If set, VPDB Agent puts itself into the Windows startup folder.
		/// </summary>
		public bool StartWithWindows { get { return _startWithWindows; } set { this.RaiseAndSetIfChanged(ref _startWithWindows, value); } }

		/// <summary>
		/// If set, XMLs are written by the serializer and pretty-printed. 
		/// Otherwise, we string-replace stuff the ugly way.
		/// </summary>
		public bool ReformatXml { get { return _reformatXml; } set { this.RaiseAndSetIfChanged(ref _reformatXml, value); } }

		/// <summary>
		/// Which XML file should be updated. Key is name of the platform (see 
		/// <see cref="Data.Objects.PlatformType"/>), value is file name without 
		/// extension.
		/// </summary>
		public Dictionary<Platform, string> XmlFile { get { return _xmlFile; } set { this.RaiseAndSetIfChanged(ref _xmlFile, value); } }

		/// <summary>
		/// If true, download all starred/synced releases on startup.
		/// </summary>
		public bool DownloadOnStartup { get { return _downloadOnStartup; } set { this.RaiseAndSetIfChanged(ref _downloadOnStartup, value); } }

		/// <summary>
		/// If true, changes of a local table script are applied to table updates using three-way merge.
		/// </summary>
		public bool PatchTableScripts { get { return _patchTableScripts; } set { this.RaiseAndSetIfChanged(ref _patchTableScripts, value); } }

		/// <summary>
		/// Primary orientation when downloading a release
		/// </summary>
		public SettingsManager.Orientation DownloadOrientation { get { return _downloadOrientation; } set { this.RaiseAndSetIfChanged(ref _downloadOrientation, value); } }

		/// <summary>
		/// If primary orientation is not available, use this if available (otherwise, ignore)
		/// </summary>
		public SettingsManager.Orientation DownloadOrientationFallback { get { return _downloadOrientationFallback; } set { this.RaiseAndSetIfChanged(ref _downloadOrientationFallback, value); } }

		/// <summary>
		/// Primary lighting flavor when downloading a release
		/// </summary>
		public SettingsManager.Lighting DownloadLighting { get { return _downloadLighting; } set { this.RaiseAndSetIfChanged(ref _downloadLighting, value); } }

		/// <summary>
		/// If primary lighting is not available, use this if available (otherwise, ignore)
		/// </summary>
		public SettingsManager.Lighting DownloadLightingFallback { get { return _downloadLightingFallback; } set { this.RaiseAndSetIfChanged(ref _downloadLightingFallback, value); } }

		/// <summary>
		/// If primary lighting is not available, use this if available (otherwise, ignore)
		/// </summary>
		public Position WindowPosition { get { return _position; } set { this.RaiseAndSetIfChanged(ref _position, value); } }

		/// <summary>
		/// Only true until settings are saved for the first time.
		/// </summary>
		public bool IsFirstRun { get { return _isFirstRun; } set { this.RaiseAndSetIfChanged(ref _isFirstRun, value); } }

		/// <summary>
		/// True if validated, false otherwise.
		/// </summary>
		/// <remarks>
		/// The only property that is obviously not persisted.
		/// </remarks>
		public bool IsValidated { get; protected internal set; } = false;


		private string _apiKey;
		private string _authUser;
		private string _authPass;
		private string _endpoint;
		private string _pbxFolder;
		private bool _syncStarred;
		private bool _minimizeToTray;
		private bool _startWithWindows;
		private bool _reformatXml;
		private Dictionary<Platform, string> _xmlFile;
		private bool _downloadOnStartup;
		private bool _patchTableScripts;
		private SettingsManager.Orientation _downloadOrientation;
		private SettingsManager.Orientation _downloadOrientationFallback;
		private SettingsManager.Lighting _downloadLighting;
		private SettingsManager.Lighting _downloadLightingFallback;
		private Position _position;
		private bool _isFirstRun;

		public Settings Copy()
		{
			return Copy(this, new Settings());
		}

		public async Task ReadFromStorage(IBlobCache storage)
		{
			ApiKey = await storage.GetOrCreateObject("ApiKey", () => "");
			AuthUser = await storage.GetOrCreateObject("AuthUser", () => "");
			AuthPass = await storage.GetOrCreateObject("AuthPass", () => "");
			Endpoint = await storage.GetOrCreateObject("Endpoint", () => "https://api.vpdb.io");
			PbxFolder = await storage.GetOrCreateObject("PbxFolder", () => "");
			SyncStarred = await storage.GetOrCreateObject("SyncStarred", () => true);
			MinimizeToTray = await storage.GetOrCreateObject("MinimizeToTray", () => false);
			StartWithWindows = await storage.GetOrCreateObject("StartWithWindows", () => false);
			ReformatXml = await storage.GetOrCreateObject("ReformatXml", () => false);
			XmlFile = await storage.GetOrCreateObject("XmlFile", () => new Dictionary<Platform, string> {{ Platform.VP, "Visual Pinball" }});
			DownloadOnStartup = await storage.GetOrCreateObject("DownloadOnStartup", () => false);
			PatchTableScripts = await storage.GetOrCreateObject("PatchTableScripts", () => true);
			DownloadOrientation = await storage.GetOrCreateObject("DownloadOrientation", () => SettingsManager.Orientation.Portrait);
			DownloadOrientationFallback = await storage.GetOrCreateObject("DownloadOrientationFallback", () => SettingsManager.Orientation.Same);
			DownloadLighting = await storage.GetOrCreateObject("DownloadLighting", () => SettingsManager.Lighting.Day);
			DownloadLightingFallback = await storage.GetOrCreateObject("DownloadLightingFallback", () => SettingsManager.Lighting.Any);
			WindowPosition = await storage.GetOrCreateObject("WindowPosition", () => new Position());
			IsFirstRun = await storage.GetOrCreateObject("IsFirstRun", () => true);
		}

		public async Task WriteToStorage(IBlobCache storage)
		{
			await storage.InsertObject("ApiKey", ApiKey);
			await storage.InsertObject("AuthUser", AuthUser);
			await storage.InsertObject("AuthPass", AuthPass);
			await storage.InsertObject("Endpoint", Endpoint);
			await storage.InsertObject("PbxFolder", PbxFolder);
			await storage.InsertObject("SyncStarred", SyncStarred);
			await storage.InsertObject("MinimizeToTray", MinimizeToTray);
			await storage.InsertObject("StartWithWindows", StartWithWindows);
			await storage.InsertObject("ReformatXml", ReformatXml);
			await storage.InsertObject("XmlFile", XmlFile);
			await storage.InsertObject("DownloadOnStartup", DownloadOnStartup);
			await storage.InsertObject("PatchTableScripts", PatchTableScripts);
			await storage.InsertObject("DownloadOrientation", DownloadOrientation);
			await storage.InsertObject("DownloadOrientationFallback", DownloadOrientationFallback);
			await storage.InsertObject("DownloadLighting", DownloadLighting);
			await storage.InsertObject("DownloadLightingFallback", DownloadLightingFallback);
			await storage.InsertObject("IsFirstRun", false);
			IsFirstRun = false;
		}

		public async Task WriteInternalToStorage(IBlobCache storage) {
			await storage.InsertObject("WindowPosition", WindowPosition);
		}

		protected internal static Settings Copy(Settings from, Settings to)
		{
			to.ApiKey = from.ApiKey;
			to.AuthUser = from.AuthUser;
			to.AuthPass = from.AuthPass;
			to.Endpoint = from.Endpoint;
			to.PbxFolder = from.PbxFolder;
			to.SyncStarred = from.SyncStarred;
			to.MinimizeToTray = from.MinimizeToTray;
			to.StartWithWindows = from.StartWithWindows;
			to.ReformatXml = from.ReformatXml;
			to.XmlFile = new Dictionary<Platform, string>(from.XmlFile);
			to.DownloadOnStartup = from.DownloadOnStartup;
			to.PatchTableScripts = from.PatchTableScripts;
			to.DownloadOrientation = from.DownloadOrientation;
			to.DownloadOrientationFallback = from.DownloadOrientationFallback;
			to.DownloadLighting = from.DownloadLighting;
			to.DownloadLightingFallback = from.DownloadLightingFallback;
			to.WindowPosition = from.WindowPosition;
			return to;
		}

		public class Position
		{
			public double Top { get; set; } = -1;
			public double Left { get; set; } = -1;
			public double Height { get; set; } = 730;
			public double Width { get; set; } = 900;
			public bool Max { get; set; } = false;
		}
	}
}
