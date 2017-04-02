using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ReactiveUI;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbRelease : ReactiveObject
	{
		private bool _starred;
		private string _name;
		private ReactiveList<VpdbVersion> _versions;
		private VpdbThumb _thumb;

		[DataMember] public string Id { get; set; }
		[DataMember] public string Name { get { return _name; } set { this.RaiseAndSetIfChanged(ref _name, value); } }
		[DataMember] public DateTime CreatedAt { get; set; }
		[DataMember] public List<VpdbAuthor> Authors { get; set; }
		[DataMember] public ReleaseCounter Counter { get; set; }
		[DataMember] public VpdbGame Game { get; set; }

		[DataMember] public VpdbThumb Thumb { get { return _thumb; } set { this.RaiseAndSetIfChanged(ref _thumb, value); } }
		[DataMember] public ReactiveList<VpdbVersion> Versions { get { return _versions; } set { this.RaiseAndSetIfChanged(ref _versions, value); } }
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

		public VpdbFile GetFile(string fileId)
		{
			foreach (var v in Versions) {
				foreach (var f in v.Files) {
					if (f.Reference.Id == fileId) {
						return f.Reference;
					}
				}
			}
			return null;
		}
		public VpdbVersion GetVersion(string fileId)
		{
			foreach (var v in Versions) {
				foreach (var f in v.Files) {
					if (f.Reference.Id == fileId) {
						return v;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Updates a release from the backend.
		/// </summary>
		/// <param name="release"></param>
		public void Update(VpdbRelease release)
		{
			// no need to update if it's the same object.
			if (ReferenceEquals(this, release)) {
				return;
			}

			Name = release.Name;
			CreatedAt = release.CreatedAt;
			Authors = release.Authors;
			Counter = release.Counter;
			Game = release.Game;
			Starred = release.Starred;

			using (Versions.SuppressChangeNotifications()) {
				foreach (var version in release.Versions) {
					var existingVersion = Versions.FirstOrDefault(v => version.Name.Equals(v.Name));
					if (existingVersion != null) {
						Versions.Remove(existingVersion);
					}
					Versions.Add(version);
				}
			}
		}

		public override string ToString()
		{
			return $"[release] {Id} {Game.DisplayName} - {Name}";
		}
	}
}