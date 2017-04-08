using Newtonsoft.Json;
using System;
using ReactiveUI;
using System.Runtime.Serialization;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbVersion : ReactiveObject
	{
		[JsonProperty(PropertyName = "version")]
		[DataMember] public string Name { get; set; }
		[DataMember] public string Changes { get; set; }
		[DataMember] public DateTime ReleasedAt { get; set; }
		[DataMember] public ReactiveList<VpdbTableFile> Files { get; set; }

		public override string ToString()
		{
			return $"v{Name}" + (Files.Count > 1 ? $" ({Files.Count} files)" : "");
		}
	}
}
