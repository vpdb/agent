using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Models
{
	public class Author
	{
		public List<string> Roles;
		public User User;

		public override string ToString()
		{
			return String.Format("{0}: {1}", User.Name, string.Join(", ", Roles));
		}
	}

	
}
