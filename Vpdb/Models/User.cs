using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Models
{
	public class User
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string Username { get; set; }
		public string GravatarId { get; set; }
		public UserCounter Counter { get; set; }

		public class UserCounter
		{
			public int Comments { get; set; }
			public int Stars { get; set; }
		}
	}
}
