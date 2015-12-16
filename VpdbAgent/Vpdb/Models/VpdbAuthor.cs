using System.Collections.Generic;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbAuthor
	{
		public List<string> Roles { get; set; }
		public VpdbUser User { get; set; }

		public override string ToString()
		{
			return $"{User.Name}: {string.Join(", ", Roles)}";
		}
	}
}
