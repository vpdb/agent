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
		public MessageType Type;
		public bool WasRead;
	}

	public enum MessageType
	{
		Info, Error
	}
}
