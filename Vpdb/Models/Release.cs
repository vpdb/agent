using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class Release : ReactiveObject
	{
		private bool starred;
		private string name;
		public Version latestVersion;

		[DataMember]
		public string Id { get; set; }
		[DataMember]
		public string Name
		{
			get { return name; }
			set { this.RaiseAndSetIfChanged(ref name, value); }
		}
		[DataMember]
		public DateTime CreatedAt { get; set; }
		[DataMember]
		public List<Author> Authors { get; set; }
		[DataMember]
		public ReleaseCounter Counter { get; set; }
		[DataMember]
		public Game Game { get; set; }
		[DataMember]
		public Version LatestVersion
		{
			get { return latestVersion; }
			set { this.RaiseAndSetIfChanged(ref latestVersion, value); }
		}
		[DataMember]
		public bool Starred
		{
			get { return starred; }
			set { this.RaiseAndSetIfChanged(ref starred, value); }
		}

		public Release()
		{
			Console.WriteLine("instantiated empty release object.");
		}

		public class ReleaseCounter
		{
			public int Comments { get; set; }
			public int Stars { get; set; }
			public int Downloads { get; set; }
		}
	}
}