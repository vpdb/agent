using System.Runtime.Serialization;
using LiteDB;
using Newtonsoft.Json;
using VpdbAgent.Application;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbGame
	{
		[DataMember] [BsonId] public string Id { get; set; }
		[DataMember] public string Title { get; set; }
		[DataMember] public string Manufacturer { get; set; }
		[DataMember] public int Year { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableFiles)] public VpdbFile Backglass { get; set; }
		[DataMember] [BsonRef(DatabaseManager.TableFiles)] public VpdbFile Logo { get; set; }

		[JsonIgnore] [BsonIgnore]
		public string DisplayName => Manufacturer != null ? $"{Title} ({Manufacturer} {Year})" : Title;
	}
}
