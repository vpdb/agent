using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Models
{
	public class Message
	{
		public DateTime CreatedAt { get; set; }
		public MessageLevel Level;
		public bool WasRead;
	    public dynamic Data;
	    public MessageType Type;

        public Message() { }

	    public Message(MessageType type, dynamic data, MessageLevel level)
	    {
	        CreatedAt = DateTime.Now;
	        Type = type;
	        Data = data;
	        Level = level;
	        WasRead = false;
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
