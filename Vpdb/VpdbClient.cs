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
		private readonly SettingsManager settingsManager = SettingsManager.GetInstance();
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		public readonly VpdbApi Api;
		public readonly Pusher Pusher;

		private byte[] authHeader;

		public VpdbClient() 
		{
			if (settingsManager.IsInitialized()) {
				authHeader = Encoding.ASCII.GetBytes(settingsManager.AuthUser + ":" + settingsManager.AuthPass);

				Uri endPoint = new Uri(settingsManager.Endpoint);
				HttpClient client = new HttpClient(new AuthenticatedHttpClientHandler(
					settingsManager.ApiKey,
					settingsManager.AuthUser,
					settingsManager.AuthPass)) { BaseAddress = endPoint };

				RefitSettings settings = new RefitSettings {
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
		}

		public WebRequest GetWebRequest(string path)
		{
			string endPoint = settingsManager.Endpoint;
			WebRequest request = WebRequest.Create(endPoint + path);
			if (settingsManager.IsInitialized()) {
				request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(authHeader));
			} else {
				logger.Warn("You probably shouldn't do requests if settings are not initialized.");
			}
			return request;
		}
	}
}
