using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Akavache;
using Newtonsoft.Json;
using NLog;
using ReactiveUI;
using Refit;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using VpdbAgent.Vpdb.Network;

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
		#region Read/Write Settings

		/// <summary>
		/// VPDB's API key
		/// </summary>
		string ApiKey { get; set; }

		/// <summary>
		/// If HTTP Basic authentication is enabled on VPDB, this is the user name.
		/// </summary>
		string AuthUser { get; set; }

		/// <summary>
		/// If HTTP Basic authentication is enabled on VPDB, this is the password.
		/// </summary>
		string AuthPass { get; set; }

		/// <summary>
		/// The endpoint of the VPDB API.
		/// </summary>
		string Endpoint { get; set; }

		/// <summary>
		/// The local folder where the user installed PinballX
		/// </summary>
		string PbxFolder { get; set; }

		/// <summary>
		/// If true, starring a release on vpdb.io will make it synced here.
		/// </summary>
		bool SyncStarred { get; set; }

		/// <summary>
		/// If true, download all starred/synced releases on startup.
		/// </summary>
		bool DownloadOnStartup { get; set; }

		/// <summary>
		/// Primary orientation when downloading a release
		/// </summary>
		SettingsManager.Orientation DownloadOrientation { get; set; }

		/// <summary>
		/// If primary orientation is not available, use this if available (otherwise, ignore)
		/// </summary>
		SettingsManager.Orientation DownloadOrientationFallback { get; set; }

		/// <summary>
		/// Primary lighting flavor when downloading a release
		/// </summary>
		SettingsManager.Lighting DownloadLighting { get; set; }

		/// <summary>
		/// If primary lighting is not available, use this if available (otherwise, ignore)
		/// </summary>
		SettingsManager.Lighting DownloadLightingFallback { get; set; }

		#endregion
		#region Read-only Settings

		/// <summary>
		/// True if the app is starting for the first time
		/// </summary>
		bool IsFirstRun { get; }

		/// <summary>
		/// False when currently saved settings are not valid, forcing the user
		/// to change and revalidate.
		/// </summary>
		bool CanCancel { get; }

		/// <summary>
		/// The currently authenticated user at VPDB
		/// </summary>
		UserFull AuthenticatedUser { get; }

		#endregion

		/// <summary>
		/// Produces a value each time settings are updated or available
		/// </summary>
		IObservable<ISettingsManager> SettingsAvailable { get; }

		/// <summary>
		/// Produces a value each time the API tried to authenticate. Value is null
		/// if authentication failed.
		/// </summary>
		IObservable<UserFull> ApiAuthenticated { get; }

		/// <summary>
		/// Validates current settings and returns a list of errors.
		/// </summary>
		/// <returns>List of validation errors or empty list if validation succeeded</returns>
		Task<Dictionary<string, string>> Validate();

		/// <summary>
		/// Persists current settings.
		/// </summary>
		/// <remarks>
		/// Only call this after <see cref="Validate"/>, since it doesn't validate on its own!
		/// </remarks>
		/// <returns>This instance</returns>
		ISettingsManager Save();

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

		public string ApiKey { get; set; }
		public string AuthUser { get; set; }
		public string AuthPass { get; set; }
		public string Endpoint { get; set; }
		public string PbxFolder { get; set; }
		public bool SyncStarred { get; set; }
		public bool DownloadOnStartup { get; set; }
		public Orientation DownloadOrientation { get; set; }
		public Orientation DownloadOrientationFallback { get; set; }
		public Lighting DownloadLighting { get; set; }
		public Lighting DownloadLightingFallback { get; set; }

		public bool IsFirstRun { get { return _isFirstRun; } set { this.RaiseAndSetIfChanged(ref _isFirstRun, value); } }
		public bool CanCancel { get { return _canCancel; } set { this.RaiseAndSetIfChanged(ref _canCancel, value); } }
		public UserFull AuthenticatedUser { get { return _authenticatedUser; } set { this.RaiseAndSetIfChanged(ref _authenticatedUser, value); } }

		private readonly Subject<UserFull> _apiAuthenticated = new Subject<UserFull>();
		private readonly BehaviorSubject<ISettingsManager> _settingsAvailable = new BehaviorSubject<ISettingsManager>(null); 

		public IObservable<UserFull> ApiAuthenticated => _apiAuthenticated;
		public IObservable<ISettingsManager> SettingsAvailable => _settingsAvailable;

		private bool _isFirstRun;
		private UserFull _authenticatedUser;
		private bool _canCancel;

		private readonly Logger _logger;
		private readonly IBlobCache _storage;

		public SettingsManager(Logger logger)
		{

			BlobCache.ApplicationName = DataFolder;
			_storage = BlobCache.Secure;

			Task.Run(async () => {

				ApiKey = await _storage.GetOrCreateObject("ApiKey", () => "");
				AuthUser = await _storage.GetOrCreateObject("AuthUser", () => "");
				AuthPass = await _storage.GetOrCreateObject("AuthPass", () => "");
				Endpoint = await _storage.GetOrCreateObject("Endpoint", () => "https://staging.vpdb.io");
				PbxFolder = await _storage.GetOrCreateObject("PbxFolder", () => "");
				SyncStarred = await _storage.GetOrCreateObject("SyncStarred", () => true);
				DownloadOnStartup = await _storage.GetOrCreateObject("DownloadOnStartup", () => false);
				DownloadOrientation = await _storage.GetOrCreateObject("DownloadOrientation", () => Orientation.Portrait );
				DownloadOrientationFallback = await _storage.GetOrCreateObject("DownloadOrientationFallback", () => Orientation.Same );
				DownloadLighting = await _storage.GetOrCreateObject("DownloadLighting", () => Lighting.Day );
				DownloadLightingFallback = await _storage.GetOrCreateObject("DownloadLightingFallback", () => Lighting.Any );
				IsFirstRun = await _storage.GetOrCreateObject("IsFirstRun", () => true);

				_settingsAvailable.OnNext(this);
			});
			_logger = logger;
		}

		public async Task<Dictionary<string, string>> Validate()
		{

			_logger.Info("Validating settings...");
			var errors = new Dictionary<string, string>();

			// pinballx folder
			if (string.IsNullOrEmpty(PbxFolder)) {
				errors.Add("PbxFolder", "The folder where PinballX is installed must be set.");
			} else if (!Directory.Exists(PbxFolder) || !Directory.Exists(PbxFolder + @"\Config")) {
				errors.Add("PbxFolder", "The folder \"" + PbxFolder + "\" is not a valid PinballX folder.");
			}

			// network params
			if (string.IsNullOrEmpty(ApiKey)) {
				errors.Add("ApiKey", "The API key is mandatory and needed in order to communicate with VPDB.");
			}
			if (string.IsNullOrEmpty(Endpoint)) {
				errors.Add("Endpoint", "The endpoint is mandatory. In doubt, put \"https://vpdb.io\".");
			}

			// test params if set
			if (!string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Endpoint)) {
				try {
					var handler = new AuthenticatedHttpClientHandler(ApiKey, AuthUser, AuthPass);
					var client = new HttpClient(handler) {BaseAddress = new Uri(Endpoint)};
					var settings = new RefitSettings { JsonSerializerSettings = new JsonSerializerSettings {ContractResolver = new SnakeCasePropertyNamesContractResolver()} };
					var api = RestService.For<IVpdbApi>(client, settings);
					var user = await api.GetProfile().SubscribeOn(Scheduler.Default).ToTask();

					_logger.Info("Logged as <{0}>", user.Email);
					OnValidationResult(user);

				} catch (ApiException e) {
					HandleApiError(errors, e);
					OnValidationResult(null);

				} catch (Exception e) {
					errors.Add("ApiKey", e.Message);
					OnValidationResult(null);
				}
			}
			return errors;
		}

		private void OnValidationResult(UserFull user)
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

		public ISettingsManager Save()
		{

			Task.Run(async () => {
				await _storage.InsertObject("ApiKey", ApiKey);
				await _storage.InsertObject("AuthUser", AuthUser);
				await _storage.InsertObject("AuthPass", AuthPass);
				await _storage.InsertObject("Endpoint", Endpoint);
				await _storage.InsertObject("PbxFolder", PbxFolder);
				await _storage.InsertObject("SyncStarred", SyncStarred);
				await _storage.InsertObject("DownloadOnStartup", DownloadOnStartup);
				await _storage.InsertObject("DownloadOrientation", DownloadOrientation);
				await _storage.InsertObject("DownloadOrientationFallback", DownloadOrientationFallback);
				await _storage.InsertObject("DownloadLighting", DownloadLighting);
				await _storage.InsertObject("DownloadLightingFallback", DownloadLightingFallback);
				await _storage.InsertObject("IsFirstRun", false);

			});

			CanCancel = true;
			IsFirstRun = false;
			return this;
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