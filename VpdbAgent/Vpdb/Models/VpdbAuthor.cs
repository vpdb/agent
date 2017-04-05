using System.Collections.Generic;
using LiteDB;
using VpdbAgent.Application;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbAuthor
	{
		public List<string> Roles { get; set; }
		[BsonRef(DatabaseManager.TableUsers)] public VpdbUser User { get; set; }

		public override string ToString()
		{
			return $"{User.Name}: {string.Join(", ", Roles)}";
		}
	}
}
