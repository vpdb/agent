using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VpdbAgent.Models
{
	public class Message
	{
		public MessageLevel Level { get; set; }
		public MessageType Type { get; set; }
		public DateTime CreatedAt { get; set; }
		public bool WasRead { get; set; }
		public Dictionary<string, string> Data;

		public Message() { }

		public Message(MessageType type, MessageLevel level, Dictionary<string, string> data)
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
		ReleaseDownloaded, ReleaseLinked, Error, ApiError
	}

	public enum MessageLevel
	{
		Info, Warning, Error
	}
}
