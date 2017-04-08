using System.Collections.Generic;
using System.Runtime.Serialization;
using LiteDB;
using VpdbAgent.Application;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbAuthor
	{
		[DataMember] public List<string> Roles { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableUsers)] public VpdbUser User { get; set; }

		public override string ToString()
		{
			return $"{User.Name}: {string.Join(", ", Roles)}";
		}
	}
}
