using Newtonsoft.Json;
using NLog;
using Refit;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

		public Subject<Channel> UserChannel { get; } = new Subject<Channel>();
		public ReactiveList<string> StarredReleaseIds { get; } = new ReactiveList<string>();

		private Channel _userChannel;
		private byte[] _authHeader;
		private UserFull _user;

		public VpdbClient(ISettingsManager settingsManager, Logger logger)
		{
			_settingsManager = settingsManager;
			_logger = logger;
		}

		public IVpdbClient Initialize()
		{
			if (!_settingsManager.IsInitialized()) {
				return this;
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
			Api = RestService.For<IVpdbApi>(client, settings);

			// retrieve user profile
			Api.GetProfile().Subscribe(user =>
			{
				_user = user;
				_logger.Info("Logged as <{0}>", user.Email);
				if (user.Permissions.Messages?.Contains("receive") == true) {
					SetupPusher(user);
				}
			}, error =>
			{
				_logger.Info("Error logging in: {0}", error.Message);
			});

			Pusher = new Pusher("02ee40b62e1fb0696e02", new PusherOptions() {
				Encrypted = true,
				Authorizer = new PusherAuthorizer(this, _logger)
			});
			return this;
		}

		public WebRequest GetWebRequest(string path)
		{
			var endPoint = _settingsManager.Endpoint;
			var request = WebRequest.Create(endPoint + path);
			if (_settingsManager.IsInitialized()) {
				if (!string.IsNullOrEmpty(_settingsManager.AuthUser)) {
					request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(_authHeader));
					request.Headers.Add("X-Authorization", "Bearer " + _settingsManager.ApiKey.Trim());
				} else {
					request.Headers.Add("Authorization", "Bearer " + _settingsManager.ApiKey.Trim());
				}
			} else {
				_logger.Warn("You probably shouldn't do requests if settings are not initialized.");
			}
			return request;
		}

		#region Pusher
		private void SetupPusher(User user)
		{
			// pusher test
			_logger.Info("Setting up Pusher...");

			Pusher.ConnectionStateChanged += PusherConnectionStateChanged;
			Pusher.Error += PusherError;

			_userChannel = Pusher.Subscribe("private-user-" + user.Id);
			_userChannel.Subscribed += PusherSubscribed;

			Pusher.Connect();
		}

		private void PusherConnectionStateChanged(object sender, ConnectionState state)
		{
			_logger.Info("Pusher connection {0}", state);
		}

		private void PusherError(object sender, PusherException error)
		{
			UserChannel.OnNext(null);
			_logger.Error(error, "Pusher error: {0}", error.Message);
		}

		private void PusherSubscribed(object sender)
		{
			UserChannel.OnNext(_userChannel);
			_logger.Info("Subscribed to channel.");
		}
		#endregion

	}

	public interface IVpdbClient
	{
		IVpdbApi Api { get; }
		Pusher Pusher { get; }
		Subject<Channel> UserChannel { get; }

		IVpdbClient Initialize();
		WebRequest GetWebRequest(string path);
	}
}
