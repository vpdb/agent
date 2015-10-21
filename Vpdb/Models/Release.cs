using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class Release : ReactiveObject
	{
		private bool _starred;
		private string _name;
		private Version _latestVersion;

		[DataMember] public string Id { get; set; }
		[DataMember] public string Name { get { return _name; } set { this.RaiseAndSetIfChanged(ref _name, value); } }
		[DataMember] public DateTime CreatedAt { get; set; }
		[DataMember] public List<Author> Authors { get; set; }
		[DataMember] public ReleaseCounter Counter { get; set; }
		[DataMember] public Game Game { get; set; }

		[DataMember] public List<Version> Versions;
		[DataMember] public Version LatestVersion { get { return _latestVersion; } set { this.RaiseAndSetIfChanged(ref _latestVersion, value); } }
		[DataMember] public bool Starred          { get { return _starred; }       set { this.RaiseAndSetIfChanged(ref _starred, value); } }

		public class ReleaseCounter
		{
			public int Comments { get; set; }
			public int Stars { get; set; }
			public int Downloads { get; set; }
		}

		/// <summary>
		/// Updates a release from the backend.
		/// </summary>
		/// <param name="release"></param>
		public void Update(Release release)
		{
			Name = release.Name;
			CreatedAt = release.CreatedAt;
			Authors = release.Authors;
			Counter = release.Counter;
			Game = release.Game;
			LatestVersion = release.LatestVersion;
			Starred = release.Starred;
		}

		public override string ToString()
		{
			return $"[release] {Id} {Game.DisplayName} - {Name}";
		}
	}
}