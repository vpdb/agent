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
		public string ApiKey { get; set; }
		public string AuthUser { get; set; }
		public string AuthPass { get; set; }
		public string Endpoint { get; set; }
		public string PbxFolder { get; set; }
		public bool IsFirstRun { get { return _isFirstRun; } set { this.RaiseAndSetIfChanged(ref _isFirstRun, value); } }
		public bool CanCancel { get { return _canCancel; } set { this.RaiseAndSetIfChanged(ref _canCancel, value); } }
		public UserFull AuthenticatedUser { get { return _authenticatedUser; } set { this.RaiseAndSetIfChanged(ref _authenticatedUser, value); } }

		private readonly Subject<UserFull> _apiAuthenticated = new Subject<UserFull>();

		public IObservable<UserFull> ApiAuthenticated => _apiAuthenticated;
		
		private bool _isFirstRun;
		private UserFull _authenticatedUser;
		private bool _canCancel;

		private readonly Logger _logger;

		public SettingsManager(Logger logger)
		{
			ApiKey = (string)Properties.Settings.Default["ApiKey"];
			AuthUser = (string)Properties.Settings.Default["AuthUser"];
			AuthPass = (string)Properties.Settings.Default["AuthPass"];
			Endpoint = (string)Properties.Settings.Default["Endpoint"];
			PbxFolder = (string)Properties.Settings.Default["PbxFolder"];
			IsFirstRun = (bool)Properties.Settings.Default["IsFirstRun"];

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
					_apiAuthenticated.OnError(e);
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
			Properties.Settings.Default["ApiKey"] = ApiKey;
			Properties.Settings.Default["AuthUser"] = AuthUser;
			Properties.Settings.Default["AuthPass"] = AuthPass;
			Properties.Settings.Default["Endpoint"] = Endpoint;
			Properties.Settings.Default["PbxFolder"] = PbxFolder;
			Properties.Settings.Default["IsFirstRun"] = false;
			Properties.Settings.Default.Save();

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
		IObservable<UserFull> ApiAuthenticated { get; }

		Task<Dictionary<string, string>> Validate();
		ISettingsManager Save();
		Dictionary<string, string> OnApiFailed(ApiException apiException);
	}
}