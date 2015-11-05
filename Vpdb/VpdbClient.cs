using Newtonsoft.Json;
using NLog;
using Refit;
using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using VpdbAgent.Vpdb.Network;
using PusherClient;
using VpdbAgent.Application;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Vpdb
{
	/// <summary>
	/// VPDB's networking layer
	/// </summary>
	/// <remarks>
	/// Sets up API access, the Pusher connection and has factory methods for
	/// retrieving <see cref="WebClient"/> and <see cref="WebRequest"/>
	/// instances with enabled authentification.
	/// </remarks>
	public interface IVpdbClient
	{
		/// <summary>
		/// Access to the VPDB API
		/// </summary>
		IVpdbApi Api { get; }

		/// <summary>
		/// Full profile of the currently logged user
		/// </summary>
		UserFull User { get; }

		/// <summary>
		/// Access to Pusher's user channel
		/// </summary>
		Subject<Channel> UserChannel { get; }

		/// <summary>
		/// Initializes the client.
		///    1. Retrieves user profile from VPDB using configured credentials
		///    2. Connects to Pusher
		/// </summary>
		/// <returns>IVpdbClient instance</returns>
		IVpdbClient Initialize();

		/// <summary>
		/// Returns a new <see cref="WebRequest"/> object with added 
		/// authorization header.
		/// </summary>
		/// <param name="path">Relative path of the URL</param>
		/// <returns>Authenticated web request object</returns>
		WebRequest GetWebRequest(string path);

		/// <summary>
		/// Returns a new <see cref="WebClient"/> object with added 
		/// authorization header.
		/// </summary>
		/// <returns>Authenticated web client object</returns>
		WebClient GetWebClient();

		/// <summary>
		/// Returns a full Uri based on a given absolute path.
		/// </summary>
		/// <param name="path">Absolute path</param>
		/// <returns>Uri with including hostname</returns>
		Uri GetUri(string path);
	}

	/// <summary>
	/// Application logic for <see cref="IVpdbClient"/>.
	/// </summary>
	public class VpdbClient : IVpdbClient
	{
		// dependencies
		private readonly ISettingsManager _settingsManager;
		private readonly Logger _logger;

		// api
		public IVpdbApi Api { get; private set; }
		public UserFull User { get; private set; }
		public Subject<Channel> UserChannel { get; } = new Subject<Channel>();

		// private members
		private Pusher _pusher;
		private Channel _userChannel;

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
			
			// setup rest client
			var handler = new AuthenticatedHttpClientHandler(_settingsManager.ApiKey, _settingsManager.AuthUser, _settingsManager.AuthPass) {
				// todo enable gzip in api!!
//				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
			};
			var client = new HttpClient(handler) {
				BaseAddress = new Uri(_settingsManager.Endpoint)
			};
			var settings = new RefitSettings {
				JsonSerializerSettings = new JsonSerializerSettings {
					ContractResolver = new SnakeCasePropertyNamesContractResolver()
				}
			};
			Api = RestService.For<IVpdbApi>(client, settings);

			// retrieve user profile
			Api.GetProfile().SubscribeOn(Scheduler.Default).Subscribe(user => {
				User = user;
				_logger.Info("Logged as <{0}>", user.Email);
				if (user.Permissions.Messages?.Contains("receive") == true) {
					SetupPusher(user);
				}
			}, error => {
				_logger.Error(error, "Error logging in: {0}", error.Message);
			});

			// initialize pusher
			_pusher = new Pusher("02ee40b62e1fb0696e02", new PusherOptions() {
				Encrypted = true,
				Authorizer = new PusherAuthorizer(this, _logger)
			});

			return this;
		}

		public WebRequest GetWebRequest(string path)
		{
			var uri = GetUri(path);
			_logger.Info("Creating new web request for {0}", uri.AbsolutePath);
			var request = (HttpWebRequest)WebRequest.Create(uri);
			AddHeaders(request.Headers);
			request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			return request;
		}

		public WebClient GetWebClient()
		{
			var client = new TunedWebClient();
			AddHeaders(client.Headers);
			return client;
		}

		public Uri GetUri(string path)
		{
			return new Uri(_settingsManager.Endpoint + path);
		}

		/// <summary>
		/// Adds authentication headers based on the user's settings.
		/// </summary>
		/// <param name="headers">Current headers</param>
		private void AddHeaders(NameValueCollection headers)
		{
			if (_settingsManager.IsInitialized()) {
				if (!string.IsNullOrEmpty(_settingsManager.AuthUser)) {
					var authHeader = Encoding.ASCII.GetBytes(_settingsManager.AuthUser + ":" + _settingsManager.AuthPass);
					headers.Add("Authorization", "Basic " + Convert.ToBase64String(authHeader));
					headers.Add("X-Authorization", "Bearer " + _settingsManager.ApiKey.Trim());
				} else {
					headers.Add("Authorization", "Bearer " + _settingsManager.ApiKey.Trim());
				}
			} else {
				_logger.Warn("You probably shouldn't do requests if settings are not initialized.");
			}
//			headers.Add("Accept-Encoding", "gzip,deflate");
		}

		/// <summary>
		/// A web client that uses Gzip compression by default.
		/// </summary>
		public class TunedWebClient : WebClient
		{
			protected override WebRequest GetWebRequest(Uri address)
			{
				var request = (HttpWebRequest)base.GetWebRequest(address);
				if (request == null) {
					throw new InvalidOperationException("Must use HttpWebRequest!");
				}
				request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
				return request;
			}
		}

		#region Pusher

		/// <summary>
		/// Connects to Pusher and subscribes to the user's private channel.
		/// </summary>
		/// <param name="user"></param>
		private void SetupPusher(User user)
		{
			// pusher test
			_logger.Info("Setting up Pusher...");

			_pusher.ConnectionStateChanged += PusherConnectionStateChanged;
			_pusher.Error += PusherError;

			_userChannel = _pusher.Subscribe("private-user-" + user.Id);
			_userChannel.Subscribed += PusherSubscribed;

			_pusher.Connect();
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
}
