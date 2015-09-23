using Newtonsoft.Json;
using NLog;
using Refit;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using VpdbAgent.Vpdb.Network;
using PusherClient;

namespace VpdbAgent.Vpdb
{
	public class VpdbClient
	{
		private readonly SettingsManager _settingsManager = SettingsManager.GetInstance();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public readonly VpdbApi Api;
		public readonly Pusher Pusher;

		private static VpdbClient _instance;

		private readonly byte[] _authHeader;

		private VpdbClient()
		{
			if (!_settingsManager.IsInitialized()) {
				return;
			}
			_authHeader = Encoding.ASCII.GetBytes(_settingsManager.AuthUser + ":" + _settingsManager.AuthPass);

			var endPoint = new Uri(_settingsManager.Endpoint);
			var client = new HttpClient(new AuthenticatedHttpClientHandler(
				_settingsManager.ApiKey,
				_settingsManager.AuthUser,
				_settingsManager.AuthPass)) { BaseAddress = endPoint };

			var settings = new RefitSettings {
				JsonSerializerSettings = new JsonSerializerSettings {
					ContractResolver = new SnakeCasePropertyNamesContractResolver()
				}
			};
			Api = RestService.For<VpdbApi>(client, settings);

			Pusher = new Pusher("02ee40b62e1fb0696e02", new PusherOptions() {
				Encrypted = true
				//Authorizer = new HttpAuthorizer("http://localhost:8888/auth/")
			});
		}

		public WebRequest GetWebRequest(string path)
		{
			var endPoint = _settingsManager.Endpoint;
			var request = WebRequest.Create(endPoint + path);
			if (_settingsManager.IsInitialized()) {
				request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(_authHeader));
			} else {
				Logger.Warn("You probably shouldn't do requests if settings are not initialized.");
			}
			return request;
		}

		public static VpdbClient GetInstance()
		{
			return _instance ?? (_instance = new VpdbClient());
		}
	}
}
