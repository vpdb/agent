using LiteDB;
using Newtonsoft.Json;
using VpdbAgent.Application;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbGame
	{
		public string Id { get; set; }
		public string Title { get; set; }
		public string Manufacturer { get; set; }
		public int Year { get; set; }
		[BsonRef(DatabaseManager.TableFiles)] public VpdbFile Backglass { get; set; }
		[BsonRef(DatabaseManager.TableFiles)] public VpdbFile Logo { get; set; }

		[JsonIgnore] [BsonIgnore]
		public string DisplayName => Manufacturer != null ? $"{Title} ({Manufacturer} {Year})" : Title;
	}
}
