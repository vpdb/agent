using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Models
{
	public class User
	{
		public string Id;
		public string Name;
		public string Username;
		public string GravatarId;
		public UserCounter Counter;

		public class UserCounter
		{
			public int Comments { get; set; }
			public int Stars { get; set; }
		}
	}
}
