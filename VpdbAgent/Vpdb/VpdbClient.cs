using Newtonsoft.Json;
using NLog;
using Refit;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Text;
using VpdbAgent.Vpdb.Network;
using PusherClient;
using ReactiveUI;
using VpdbAgent.Application;
using VpdbAgent.ViewModels.Settings;
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
		/// Access to Pusher's user channel
		/// </summary>
		IObservable<Channel> UserChannel { get; }

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
		/// Returns a dictionary with the auth headers computed with the
		/// user settings.
		/// </summary>
		/// <returns>Authorization headers</returns>
		IDictionary<string, string> GetAuthHeaders();

		/// <summary>
		/// Returns a full Uri based on a given absolute path.
		/// </summary>
		/// <param name="path">Absolute path</param>
		/// <returns>Uri with including hostname</returns>
		Uri GetUri(string path);

		/// <summary>
		/// Logs a message and sends it to the crash logger if necessary
		/// </summary>
		/// <param name="e"></param>
		/// <param name="origin"></param>
		void HandleApiError(Exception e, string origin);
	}

	/// <summary>
	/// Application logic for <see cref="IVpdbClient"/>.
	/// </summary>
	public class VpdbClient : IVpdbClient
	{
		// dependencies
		private readonly ISettingsManager _settingsManager;
		private readonly IVersionManager _versionManager;
		private readonly IMessageManager _messageManager;
		private readonly ILogger _logger;
		private readonly IScreen _screen;
		private readonly CrashManager _crashManager;

		// api
		public IVpdbApi Api { get; private set; }
		public IObservable<Channel> UserChannel { get; } = new Subject<Channel>();

		// private members
		private Pusher _pusher;
		private string _connectedApiEndpoint;
		private Channel _userChannel;

		public VpdbClient(ISettingsManager settingsManager, IVersionManager versionManager, IMessageManager messageManager, 
			IScreen screen, ILogger logger, CrashManager crashManager)
		{
			_settingsManager = settingsManager;
			_versionManager = versionManager;
			_messageManager = messageManager;
			_logger = logger;
			_screen = screen;
			_crashManager = crashManager;
		}

		public IVpdbClient Initialize()
		{
			// setup rest client
			var handler = new AuthenticatedHttpClientHandler(_settingsManager.Settings.ApiKey, _settingsManager.Settings.AuthUser, _settingsManager.Settings.AuthPass);
			// todo enable gzip in api!!
			// AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate

			var client = new HttpClient(handler) {
				BaseAddress = new Uri(_settingsManager.Settings.Endpoint)
			};
			var settings = new RefitSettings {
				JsonSerializerSettings = new JsonSerializerSettings {
					ContractResolver = new SnakeCasePropertyNamesContractResolver()
				}
			};
			Api = RestService.For<IVpdbApi>(client, settings);

			// subscribe to pusher if profile allows
			_settingsManager.ApiAuthenticated.Subscribe(user => {
				if (user != null && user.Permissions.Messages?.Contains("receive") == true) {
					SetupPusher(user);
				}
			}, exception => HandleApiError(exception, "subscribing to ApiAuthenticated for Pusher"));

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

		public IDictionary<string, string> GetAuthHeaders()
		{
			var headers = AddHeaders(new NameValueCollection());
			return headers.AllKeys.ToDictionary(key => key, key => headers[key]);
		}

		public Uri GetUri(string path)
		{
			return new Uri(_settingsManager.Settings.Endpoint + path);
		}

		public void HandleApiError(Exception e, string origin)
		{
			var apiException = e as ApiException;
			if (apiException?.StatusCode == HttpStatusCode.Unauthorized) {
				var errors = _settingsManager.OnApiFailed(apiException);
				_screen.Router.Navigate.Execute(new SettingsViewModel(_screen, _settingsManager, _versionManager, null, errors));
			} else {
				if (apiException == null) {
					_messageManager.LogError(e, "API error while " + origin);
				} else {
					_messageManager.LogApiError(apiException, "API error while " + origin);
				}
			}
			_logger.Error(e, "API error while {0}:", origin);
		}

		/// <summary>
		/// Adds authentication headers based on the user's settings.
		/// </summary>
		/// <param name="headers">Current headers</param>
		private NameValueCollection AddHeaders(NameValueCollection headers)
		{
			if (!string.IsNullOrEmpty(_settingsManager.Settings.ApiKey)) {
				if (!string.IsNullOrEmpty(_settingsManager.Settings.AuthUser)) {
					var authHeader = Encoding.ASCII.GetBytes(_settingsManager.Settings.AuthUser + ":" + _settingsManager.Settings.AuthPass);
					headers.Add("Authorization", "Basic " + Convert.ToBase64String(authHeader));
					headers.Add("X-Authorization", "Bearer " + _settingsManager.Settings.ApiKey.Trim());
				} else {
					headers.Add("Authorization", "Bearer " + _settingsManager.Settings.ApiKey.Trim());
				}
			} else {
				_logger.Warn("You probably shouldn't do requests if settings are not initialized.");
			}
//			headers.Add("Accept-Encoding", "gzip,deflate");
			return headers;
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
		private void SetupPusher(VpdbUserFull user)
		{

			// initialize pusher
			if (_pusher == null && user.ChannelConfig != null) {
				_pusher = new Pusher(user.ChannelConfig.ApiKey, new PusherOptions() {
					Encrypted = true,
					Authorizer = new PusherAuthorizer(this, _crashManager, _logger)
				});
			}

			var isNewConnection = _connectedApiEndpoint == null;
			var isSameConnection = !isNewConnection && _connectedApiEndpoint.Equals(_settingsManager.Settings.Endpoint);
			var isDifferentConnection = !isNewConnection && !isSameConnection;

			if (isNewConnection && _pusher != null) {
				_logger.Info("Setting up Pusher...");

				_pusher.ConnectionStateChanged += PusherConnectionStateChanged;
				_pusher.Error += PusherError;

				_pusher.Connect();
			}

			if (isDifferentConnection) {
				_logger.Info("Unsubscribing from previous channel.");
				_userChannel.Unsubscribe();
			}

			if (_pusher != null && (isNewConnection || isDifferentConnection)) {
				_logger.Info("Subscribing to user channel.");
				_userChannel = _pusher.Subscribe("private-user-" + user.Id);
				_userChannel.Subscribed += PusherSubscribed;
			}
			
			_connectedApiEndpoint = _settingsManager.Settings.Endpoint;
		}

		private void PusherConnectionStateChanged(object sender, ConnectionState state)
		{
			_logger.Info("Pusher connection {0}", state);
		}

		private void PusherError(object sender, PusherException error)
		{
			// todo handle
			((Subject<Channel>)UserChannel).OnNext(null);
			_logger.Error(error, "Pusher error: {0}", error.Message);
		}

		private void PusherSubscribed(object sender)
		{
			// todo handle
			((Subject<Channel>)UserChannel).OnNext(_userChannel);
			_logger.Info("Subscribed to channel.");
		}
		#endregion
	}
}
 