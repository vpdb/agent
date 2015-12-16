using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Network
{
	public class AuthenticatedHttpClientHandler : HttpClientHandler
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly string _apiKey;
		private readonly string _authUser;
		private readonly string _authPass;

		public AuthenticatedHttpClientHandler(string apiKey)
		{
			_apiKey = apiKey;
		}

		public AuthenticatedHttpClientHandler(string authUser, string authPass)
		{
			_authUser = authUser;
			_authPass = authPass;
		}

		public AuthenticatedHttpClientHandler(string apiKey, string authUser, string authPass)
		{
			_apiKey = apiKey;
			_authUser = authUser;
			_authPass = authPass;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (!string.IsNullOrEmpty(_authUser)) {
				var byteArray = Encoding.ASCII.GetBytes(_authUser + ":" + _authPass);
				request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
				request.Headers.Add("X-Authorization", "Bearer " + _apiKey.Trim());
			} else {
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey.Trim());
			}
			//			request.Headers.Add("Accept-Encoding", "gzip,deflate");

			Logger.Debug("=> {0} {1}", request.Method, request.RequestUri);

			return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
		}
	}
}
