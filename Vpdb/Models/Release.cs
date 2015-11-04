using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class Release : ReactiveObject
	{
		private bool _starred;
		private string _name;
		private List<Version> _versions;
		private Thumb _thumb;

		[DataMember] public string Id { get; set; }
		[DataMember] public string Name { get { return _name; } set { this.RaiseAndSetIfChanged(ref _name, value); } }
		[DataMember] public DateTime CreatedAt { get; set; }
		[DataMember] public List<Author> Authors { get; set; }
		[DataMember] public ReleaseCounter Counter { get; set; }
		[DataMember] public Game Game { get; set; }

		[DataMember] public Thumb Thumb { get { return _thumb; } set { this.RaiseAndSetIfChanged(ref _thumb, value); } }
		[DataMember] public List<Version> Versions { get { return _versions; } set { this.RaiseAndSetIfChanged(ref _versions, value); } }
		[DataMember] public bool Starred { get { return _starred; } set { this.RaiseAndSetIfChanged(ref _starred, value); } }

		// convenience methods
		public string AuthorNames { get {
			return Authors.Count > 1 
					? string.Join(", ", Authors.Take(Authors.Count - 1).Select(a => a.User.Name)) + " & " + Authors.Last().User.Name
					: Authors.First().User.Name;
		} }

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
			Starred = release.Starred;

			release.Versions.ForEach(version =>
			{
				var existingVersion = Versions.FirstOrDefault(v => version.Name.Equals(v.Name));
				if (existingVersion != null) {
					Versions.Remove(existingVersion);
				}
				Versions.Add(version);
			});
		}

		public override string ToString()
		{
			return $"[release] {Id} {Game.DisplayName} - {Name}";
		}
	}
}