using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using Refit;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Network;

namespace VpdbAgent.Application
{
	public class SettingsManager : ISettingsManager
	{
		public string ApiKey { get; set; }
		public string AuthUser { get; set; }
		public string AuthPass { get; set; }
		public string Endpoint { get; set; }
		public string PbxFolder { get; set; }

		private readonly Subject<string> _pbxFolderChanged = new Subject<string>();
		private readonly Subject<Unit> _apiChanged = new Subject<Unit>();

		public IObservable<string> PbxFolderChanged => _pbxFolderChanged;
		public IObservable<Unit> ApiChanged => _apiChanged;
		public IObservable<EventPattern<PropertyChangedEventArgs>> Changed { get; }

		public SettingsManager()
		{
			ApiKey = (string)Properties.Settings.Default["ApiKey"];
			AuthUser = (string)Properties.Settings.Default["AuthUser"];
			AuthPass = (string)Properties.Settings.Default["AuthPass"];
			Endpoint = (string)Properties.Settings.Default["Endpoint"];
			PbxFolder = (string)Properties.Settings.Default["PbxFolder"];

			Changed = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
				handler => handler.Invoke,
				ev => Properties.Settings.Default.PropertyChanged += ev,
				ev => Properties.Settings.Default.PropertyChanged -= ev);

			Changed
				.Where(args => args.EventArgs.PropertyName.Equals("PbxFolder"))
				.Select(args => PbxFolder)
				.Subscribe(_pbxFolderChanged);

			Changed
				.Where(args => new[] { "ApiKey", "AuthUser", "AuthPass", "Endpoint" }.Contains(args.EventArgs.PropertyName))
				.Select(args => Unit.Default)
				.Subscribe(_apiChanged);
		}

		public bool IsInitialized()
		{
			return !string.IsNullOrEmpty(PbxFolder);
		}

		public async Task<Dictionary<string, string>> Validate()
		{
			var errors = new Dictionary<string, string>();

			// pinballx folder
			if (string.IsNullOrEmpty(PbxFolder)) {
				errors.Add("PbxFolder", "The folder where PinballX is installed must be set.");
			} else if (!Directory.Exists(PbxFolder) || !Directory.Exists(PbxFolder + @"\Config")) {
				errors.Add("PbxFolder", "The folder \"" + PbxFolder + "\" is not a valid PinballX folder.");
			}

			// network params
			if (string.IsNullOrEmpty(ApiKey)) {
				errors.Add("ApiKey", "The API is mandatory and needed in order to communicate with VPDB.");
			}
			if (string.IsNullOrEmpty(Endpoint)) {
				errors.Add("Endpoint", "The endpoint is mandatory. In doubt, put \"https://vpdb.io\".");
			}

			// test params if set
			if (!string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Endpoint)) {
				try
				{
					var handler = new AuthenticatedHttpClientHandler(ApiKey, AuthUser, AuthPass);
					var client = new HttpClient(handler) { BaseAddress = new Uri(Endpoint) };
					var settings = new RefitSettings { JsonSerializerSettings = new JsonSerializerSettings { ContractResolver = new SnakeCasePropertyNamesContractResolver() } };
					var api = RestService.For<IVpdbApi>(client, settings);
					var user = await api.GetProfile().SubscribeOn(Scheduler.Default).ToTask();


				} catch (ApiException e) {
					if (e.HasContent && e.Content.StartsWith("<html")) {
						errors.Add("Auth", "Access denied to VPDB. Seems like the site is protected and you need to put additional credentials in here.");
					}
					Console.WriteLine("Error! {0}", e);
				} catch (Exception e) {
					errors.Add("ApiKey", e.Message);
				}
			}

			return errors;
		}

		public SettingsManager Save()
		{
			Properties.Settings.Default["ApiKey"] = ApiKey;
			Properties.Settings.Default["AuthUser"] = AuthUser;
			Properties.Settings.Default["AuthPass"] = AuthPass;
			Properties.Settings.Default["Endpoint"] = Endpoint;
			Properties.Settings.Default["PbxFolder"] = PbxFolder;
			Properties.Settings.Default.Save();
			return this;
		}

	}

	public interface ISettingsManager
	{
		string ApiKey { get; set; }
		string AuthUser { get; set; }
		string AuthPass { get; set; }
		string Endpoint { get; set; }
		string PbxFolder { get; set; }

		bool IsInitialized();
		Task<Dictionary<string, string>> Validate();
		SettingsManager Save();
	}
}