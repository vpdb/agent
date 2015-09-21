using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Network
{
	public class AuthenticatedHttpClientHandler : HttpClientHandler
	{
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		private readonly string apiKey;
		private readonly string authUser;
		private readonly string authPass;

		public AuthenticatedHttpClientHandler(string apiKey)
		{
			this.apiKey = apiKey;
		}

		public AuthenticatedHttpClientHandler(string authUser, string authPass)
		{
			this.authUser = authUser;
			this.authPass = authPass;
		}

		public AuthenticatedHttpClientHandler(string apiKey, string authUser, string authPass)
		{
			this.apiKey = apiKey;
			this.authUser = authUser;
			this.authPass = authPass;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{

			if (authUser != null && authUser.Length > 0)
			{
				var byteArray = Encoding.ASCII.GetBytes(authUser + ":" + authPass);
				request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
				request.Headers.Add("X-Authorization", apiKey);
			} else {
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
			}
			logger.Debug("=> {0} {1}", request.Method, request.RequestUri);
			
			return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
		}
	}
}
