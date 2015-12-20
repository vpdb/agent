using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Akavache;
using Microsoft.Win32;
using Newtonsoft.Json;
using NLog;
using ReactiveUI;
using Refit;
using VpdbAgent.Libs.ShellLink;
using VpdbAgent.Models;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using VpdbAgent.Vpdb.Network;
using File = System.IO.File;

namespace VpdbAgent.Application
{

	/// <summary>
	/// An API for saving and retrieving settings.
	/// </summary>
	/// <remarks>
	/// We used to use .NET's settings provider, but for unknown reason it uses
	/// separate settings for every version, meaning everything gets lost on
	/// app updates.
	/// 
	/// We ended up using Akavache which we'll be also using for image caching
	/// later.
	/// </remarks>
	public interface ISettingsManager
	{
		/// <summary>
		/// The object where all the settings are stored.
		/// </summary>
		Settings Settings { get; }

		/// <summary>
		/// False when currently saved settings are not valid, forcing the user
		/// to change and revalidate.
		/// </summary>
		bool CanCancel { get; }

		/// <summary>
		/// The currently authenticated user at VPDB
		/// </summary>
		VpdbUserFull AuthenticatedUser { get; }

		/// <summary>
		/// Produces a value each time settings are updated or available.
		/// Immediately sends the current settings on subscription if available.
		/// </summary>
		IObservable<Settings> SettingsAvailable { get; }

		/// <summary>
		/// Produces a value each time the API tried to authenticate. Value is null
		/// if authentication failed.
		/// </summary>
		IObservable<VpdbUserFull> ApiAuthenticated { get; }

		/// <summary>
		/// Validates current settings and returns a list of errors.
		/// </summary>
		/// <param name="settings">Settings to validate</param>
		/// <param name="messageManager">If given, log to messages on error</param>
		/// <returns>List of validation errors or empty list if validation succeeded</returns>
		Task<Dictionary<string, string>> Validate(Settings settings, IMessageManager messageManager = null);

		/// <summary>
		/// Persists current settings.
		/// </summary>
		/// <remarks>
		/// Only call this after <see cref="Validate"/>, since it doesn't validate on its own!
		/// </remarks>
		/// <param name="settings">Settings to save</param>
		/// <returns>An observable that returns one value when settings are saved and completes.</returns>
		IObservable<Settings> Save(Settings settings);

		/// <summary>
		/// Persists internal (non-validated) settings.
		/// </summary>
		/// <param name="settings">Settings to save</param>
		/// <returns>An observable returning the result after successfully saving.</returns>
		IObservable<Settings> SaveInternal(Settings settings);

		/// <summary>
		/// Returns a list of validation errors based on a given exception
		/// </summary>
		/// <param name="apiException">Exception to convert to validation error</param>
		/// <returns>List of (usually only one) validation error(s)</returns>
		Dictionary<string, string> OnApiFailed(ApiException apiException);
	}

	public class SettingsManager : ReactiveObject, ISettingsManager
	{
		public const string DataFolder = "VPDB";

		public Settings Settings { get; } = new Settings();

		public bool CanCancel { get { return _canCancel; } set { this.RaiseAndSetIfChanged(ref _canCancel, value); } }
		public VpdbUserFull AuthenticatedUser { get { return _authenticatedUser; } set { this.RaiseAndSetIfChanged(ref _authenticatedUser, value); } }

		private readonly Subject<VpdbUserFull> _apiAuthenticated = new Subject<VpdbUserFull>();
		private readonly BehaviorSubject<Settings> _settingsAvailable = new BehaviorSubject<Settings>(null);
		private readonly RegistryKey _registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

		public IObservable<VpdbUserFull> ApiAuthenticated => _apiAuthenticated;
		public IObservable<Settings> SettingsAvailable => _settingsAvailable;


		private VpdbUserFull _authenticatedUser;
		private bool _canCancel;

		private readonly Logger _logger;
		private readonly IBlobCache _storage;

		public SettingsManager(Logger logger)
		{
			BlobCache.ApplicationName = DataFolder;
			_storage = BlobCache.Secure;

			Task.Run(async () => {
				await Settings.ReadFromStorage(_storage);
				_settingsAvailable.OnNext(Settings);
			});
			_logger = logger;
		}

		public async Task<Dictionary<string, string>> Validate(Settings settings, IMessageManager messageManager = null)
		{
			_logger.Info("Validating settings...");
			var errors = new Dictionary<string, string>();

			// pinballx folder
			if (string.IsNullOrEmpty(settings.PbxFolder)) {
				errors.Add("PbxFolder", "The folder where PinballX is installed must be set.");
			} else if (!Directory.Exists(settings.PbxFolder) || !Directory.Exists(settings.PbxFolder + @"\Config")) {
				errors.Add("PbxFolder", "The folder \"" + settings.PbxFolder + "\" is not a valid PinballX folder.");
			}

			// network params
			if (string.IsNullOrEmpty(settings.ApiKey)) {
				errors.Add("ApiKey", "The API key is mandatory and needed in order to communicate with VPDB.");
			}
			if (string.IsNullOrEmpty(settings.Endpoint)) {
				errors.Add("Endpoint", "The endpoint is mandatory. In doubt, put \"https://vpdb.io\".");
			}

			// xml file name
			var badFilenameChars = new Regex("[\\\\" + Regex.Escape(new string(Path.GetInvalidPathChars())) + "]");
			var filename = settings.XmlFile[Platform.PlatformType.VP];
			if (string.IsNullOrWhiteSpace(filename)) {
				errors.Add("XmlFileVP", "You need to provide a file name for the XML database.");
			} else if (badFilenameChars.IsMatch(filename)) {
				errors.Add("XmlFileVP", "That doesn't look like a valid file name!");
			} else if (filename.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) {
				errors.Add("XmlFileVP", "No need to provide the .xml extension, we'll do that!");
			}

			// test params if set
			if (!string.IsNullOrEmpty(settings.ApiKey) && !string.IsNullOrEmpty(settings.Endpoint)) {
				try {
					var handler = new AuthenticatedHttpClientHandler(settings.ApiKey, settings.AuthUser, settings.AuthPass);
					var client = new HttpClient(handler) {BaseAddress = new Uri(settings.Endpoint)};
					var s = new RefitSettings { JsonSerializerSettings = new JsonSerializerSettings {ContractResolver = new SnakeCasePropertyNamesContractResolver()} };
					var api = RestService.For<IVpdbApi>(client, s);
					var user = await api.GetProfile().SubscribeOn(Scheduler.Default).ToTask();

					_logger.Info("Logged as <{0}>", user.Email);
					OnValidationResult(user);

				} catch (ApiException e) {
					HandleApiError(errors, e);
					OnValidationResult(null);
					messageManager?.LogApiError(e, "Error while logging in");

				} catch (Exception e) {
					errors.Add("ApiKey", e.Message);
					OnValidationResult(null);
					messageManager?.LogError(e, "Error while logging in");
				}
			}
			settings.IsValidated = errors.Count == 0;

			return errors;
		}

		private void OnValidationResult(VpdbUserFull user)
		{
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				if (user != null) {
					_apiAuthenticated.OnNext(user);
					AuthenticatedUser = user;
				} else {
					_apiAuthenticated.OnNext(null);
					AuthenticatedUser = null;
				}
			});
		}

		private static Dictionary<string, string> HandleApiError(Dictionary<string, string> errors, ApiException e)
		{
			if (e.StatusCode == HttpStatusCode.Unauthorized) {
				if (e.HasContent && e.Content.StartsWith("<html")) {
					errors.Add("Auth", "Access denied to VPDB. Seems like the site is protected and you need to put additional credentials in here.");
				} else {
					errors.Add("ApiKey", "Authentication failed. Are you sure you've correctly pasted your API key?");
				}
			} else {
				errors.Add("ApiKey", e.Message);
			}
			return errors;
		}

		public IObservable<Settings> Save(Settings settings)
		{
			if (!settings.IsValidated) {
				throw new InvalidOperationException("Settings must be validated before saved.");
			}
			var result = new Subject<Settings>();

			Task.Run(async () => {
				Settings.Copy(settings, Settings);
				await Settings.WriteToStorage(_storage);

				// handle startup settings todo test!
				if (Settings.StartWithWindows)
				{
					var linkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "VPDB", "VPDB Agent.lnk");
					string cmd;
					if (File.Exists(linkPath)) {
						var lnk = new ShellShortcut(linkPath);
						cmd = $"cmd /c \"cd /d {lnk.WorkingDirectory} && {lnk.Path} {lnk.Arguments} --process-start-args \\\"--minimized\\\"\"";
					} else {
						cmd = File.Exists(linkPath) ? linkPath : Assembly.GetEntryAssembly().Location;
					}
					_registryKey.SetValue("VPDB Agent", cmd);
				} else {
					if (_registryKey.GetValue("VPDB Agent") != null) {
						_registryKey.DeleteValue("VPDB Agent");
					}
				}

				result.OnNext(Settings);
				result.OnCompleted();
			});

			CanCancel = true;
			return result;
		}

		public IObservable<Settings> SaveInternal(Settings settings)
		{
			var result = new Subject<Settings>();
			Task.Run(async () => {
				Settings.Copy(settings, Settings);
				await Settings.WriteInternalToStorage(_storage);
				result.OnNext(settings);
				result.OnCompleted();
			});
			return result;
		}

		public Dictionary<string, string> OnApiFailed(ApiException apiException)
		{
			CanCancel = false;
			return HandleApiError(new Dictionary<string, string>(), apiException);
		}

		public enum Orientation
		{
			Portrait,
			Landscape,
			Universal,
			Same,
			Any
		}

		public enum Lighting
		{
			Day,
			Night,
			Universal,
			Same,
			Any,
		}
	}
}