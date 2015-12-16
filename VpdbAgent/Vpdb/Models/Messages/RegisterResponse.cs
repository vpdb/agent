using Newtonsoft.Json;

namespace VpdbAgent.Vpdb.Models.Messages
{
	public class RegisterResponse
	{
		[JsonProperty("auth")]
		public string Auth { get; set; } 
	}
}