using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive;
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
	public class SettingsManager : ReactiveObject, ISettingsManager
	{
		public const string DataFolder = "VPDB";

		public string ApiKey { get; set; }
		public string AuthUser { get; set; }
		public string AuthPass { get; set; }
		public string Endpoint { get; set; }
		public string PbxFolder { get; set; }
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
					OnValidationResult(null, user);

				} catch (ApiException e) {
					HandleApiError(errors, e);
					OnValidationResult(e, null);

				} catch (Exception e) {
					errors.Add("ApiKey", e.Message);
					OnValidationResult(e, null);
				}
			}
			return errors;
		}

		private void OnValidationResult(Exception e, UserFull user)
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
	}

	public interface ISettingsManager
	{
		string ApiKey { get; set; }
		string AuthUser { get; set; }
		string AuthPass { get; set; }
		string Endpoint { get; set; }
		string PbxFolder { get; set; }
		bool IsFirstRun { get; }
		bool CanCancel { get; }

		UserFull AuthenticatedUser { get; }
		IObservable<ISettingsManager> SettingsAvailable { get; }
		IObservable<UserFull> ApiAuthenticated { get; }
		
		Task<Dictionary<string, string>> Validate();
		ISettingsManager Save();
		Dictionary<string, string> OnApiFailed(ApiException apiException);
	}
}