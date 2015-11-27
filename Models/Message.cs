using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VpdbAgent.Models
{
	public class Message
	{
		[JsonConverter(typeof(StringEnumConverter))]
		public MessageLevel Level;
		[JsonConverter(typeof(StringEnumConverter))]
		public MessageType Type;
		public DateTime CreatedAt { get; set; }
		public bool WasRead;
		public dynamic Data;

		public Message() { }

		public Message(MessageType type, dynamic data, MessageLevel level)
		{
			CreatedAt = DateTime.Now;
			Type = type;
			Data = data;
			Level = level;
			WasRead = false;
		}

		public int CompareTo(Message message)
		{
			return -CreatedAt.CompareTo(message.CreatedAt);
		}
	}

	public enum MessageType
	{
		ReleaseLinked
	}

	public enum MessageLevel
	{
		Info, Warning, Error
	}
}
