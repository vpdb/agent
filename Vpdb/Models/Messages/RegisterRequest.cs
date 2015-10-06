using Newtonsoft.Json;

namespace VpdbAgent.Vpdb.Models.Messages
{
	public class RegisterRequest
	{
		[JsonProperty("channel_name")]
		public string ChannelName { get; set; }

		[JsonProperty("socket_id")]
		public string SocketId { get; set; }

		public RegisterRequest(string channelName, string socketId)
		{
			ChannelName = channelName;
			SocketId = socketId;
		}
	}
}