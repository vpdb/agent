using Newtonsoft.Json;
using Refit;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using VpdbAgent.Vpdb.Network;

namespace VpdbAgent.Vpdb
{
	public class VpdbClient
	{

		public readonly VpdbApi Api;

		private byte[] authHeader = Encoding.ASCII.GetBytes((string)Properties.Settings.Default["AuthUser"] + ":" + (string)Properties.Settings.Default["AuthPass"]);

		public VpdbClient() 
		{

			Uri endPoint = new Uri((string)Properties.Settings.Default["Endpoint"]);
			HttpClient client = new HttpClient(new AuthenticatedHttpClientHandler(
				(string)Properties.Settings.Default["ApiKey"], 
				(string)Properties.Settings.Default["AuthUser"], 
				(string)Properties.Settings.Default["AuthPass"])) { BaseAddress = endPoint };

			RefitSettings settings = new RefitSettings {
				JsonSerializerSettings = new JsonSerializerSettings {
					ContractResolver = new SnakeCasePropertyNamesContractResolver()
				}
			};
			Api = RestService.For<VpdbApi>(client, settings);
		}

		public WebRequest GetWebRequest(string path)
		{
			string endPoint = (string)Properties.Settings.Default["Endpoint"];
			WebRequest request = WebRequest.Create(endPoint + path);
			request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(authHeader));
			return request;
		}
	}
}
