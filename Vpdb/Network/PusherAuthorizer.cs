using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using NLog.LayoutRenderers;
using PusherClient;
using VpdbAgent.Vpdb.Models.Messages;

namespace VpdbAgent.Vpdb.Network
{
	public class PusherAuthorizer : IAuthorizer
	{
		private const string Endpoint = "/api/v1/messages/authenticate";

		// deps
		private readonly IVpdbClient _vpdbClient;
		private readonly Logger _logger;

		private readonly static JsonSerializer Serializer = new JsonSerializer {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new SnakeCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};

		public PusherAuthorizer(IVpdbClient vpdbClient, Logger logger)
		{
			_vpdbClient = vpdbClient;
			_logger = logger;
		}

		public string Authorize(string channelName, string socketId)
		{
			Stream dataStream = null;
			StreamReader reader = null;
			WebResponse response = null;
			try {

				var body = JsonConvert.SerializeObject(new RegisterRequest(channelName, socketId));
				var bodyData = Encoding.UTF8.GetBytes(body);

				var webRequest = _vpdbClient.GetWebRequest(Endpoint);
				webRequest.Method = "POST";
				webRequest.ContentType = "application/json";
				webRequest.ContentLength = bodyData.Length;
				dataStream = webRequest.GetRequestStream();
				dataStream.Write(bodyData, 0, bodyData.Length);
				dataStream.Close();
				response = webRequest.GetResponse();
				dataStream = response.GetResponseStream();
				reader = new StreamReader(dataStream);

				var resp = JsonConvert.DeserializeObject<RegisterResponse>(reader.ReadToEnd());

				return resp.Auth;

			} catch (Exception e) {
				_logger.Error(e, "Error retrieving pusher auth token.");

			} finally {
				reader?.Close();
				dataStream?.Close();
				response?.Close();
			}
			return null;
		}
	}
}
