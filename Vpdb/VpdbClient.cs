using Newtonsoft.Json;
using NLog;
using Refit;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using VpdbAgent.Vpdb.Network;
using PusherClient;
using ReactiveUI;
using Splat;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Vpdb
{
	public class VpdbClient : IVpdbClient
	{
		// dependencies
		private readonly ISettingsManager _settingsManager;
		private readonly Logger _logger;

		public IVpdbApi Api { get; private set; }
		public Pusher Pusher { get; private set; }

		private byte[] _authHeader;
		private UserFull _user;

		public ReactiveList<string> StarredReleaseIds { get; } = new ReactiveList<string>();

		public VpdbClient(ISettingsManager settingsManager, Logger logger)
		{
			_settingsManager = settingsManager;
			_logger = logger;
		}

		public IVpdbClient Initialize()
		{
			if (!_settingsManager.IsInitialized())
			{
				return this;
			}

			_authHeader = Encoding.ASCII.GetBytes(_settingsManager.AuthUser + ":" + _settingsManager.AuthPass);

			var endPoint = new Uri(_settingsManager.Endpoint);
			var client = new HttpClient(new AuthenticatedHttpClientHandler(
				_settingsManager.ApiKey,
				_settingsManager.AuthUser,
				_settingsManager.AuthPass)) {BaseAddress = endPoint};

			var settings = new RefitSettings
			{
				JsonSerializerSettings = new JsonSerializerSettings
				{
					ContractResolver = new SnakeCasePropertyNamesContractResolver()
				}
			};
			Api = RestService.For<IVpdbApi>(client, settings);

			Api.GetProfile().Subscribe(user => {
				_user = user;
				_logger.Info("Logged as <{0}>", user.Email);
			}, error =>
			{
				_logger.Info("Error logging in: {0}", error.Message);
			});


			Pusher = new Pusher("02ee40b62e1fb0696e02", new PusherOptions() {
				Encrypted = true
				//Authorizer = new HttpAuthorizer("http://localhost:8888/auth/")
			});
			return this;
		}

		public WebRequest GetWebRequest(string path)
		{
			var endPoint = _settingsManager.Endpoint;
			var request = WebRequest.Create(endPoint + path);
			if (_settingsManager.IsInitialized()) {
				request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(_authHeader));
			} else {
				_logger.Warn("You probably shouldn't do requests if settings are not initialized.");
			}
			return request;
		}

		#region Pusher
		private void SetupPusher(string userId)
		{
			// pusher test
			_logger.Info("Setting up pusher...");

			Pusher.ConnectionStateChanged += PusherConnectionStateChanged;
			Pusher.Error += PusherError;

			var testChannel = Pusher.Subscribe("test-channel");
			testChannel.Subscribed += PusherSubscribed;

			// inline binding
			testChannel.Bind("test-message", (dynamic data) =>
			{
				_logger.Info("[{0}]: {1}", data.name, data.message);
			});

			Pusher.Connect();
		}

		private void PusherConnectionStateChanged(object sender, ConnectionState state)
		{
			_logger.Info("Pusher connection {0}", state);
		}

		private void PusherError(object sender, PusherException error)
		{
			_logger.Error(error, "Pusher error!");
		}

		private void PusherSubscribed(object sender)
		{
			_logger.Info("Subscribed to channel.");
		}
		#endregion

	}

	public interface IVpdbClient
	{
		IVpdbApi Api { get; }
		Pusher Pusher { get; }

		IVpdbClient Initialize();
		WebRequest GetWebRequest(string path);
	}
}
