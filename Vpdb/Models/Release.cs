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
		private List<Version> _versions;
		private ReleaseThumb _thumb;

		[DataMember] public string Id { get; set; }
		[DataMember] public string Name { get { return _name; } set { this.RaiseAndSetIfChanged(ref _name, value); } }
		[DataMember] public DateTime CreatedAt { get; set; }
		[DataMember] public List<Author> Authors { get; set; }
		[DataMember] public ReleaseCounter Counter { get; set; }
		[DataMember] public Game Game { get; set; }

		[DataMember] public ReleaseThumb Thumb { get { return _thumb; } set { this.RaiseAndSetIfChanged(ref _thumb, value); } }
		[DataMember] public List<Version> Versions { get { return _versions; } set { this.RaiseAndSetIfChanged(ref _versions, value); } }
		[DataMember] public bool Starred { get { return _starred; }       set { this.RaiseAndSetIfChanged(ref _starred, value); } }

		public class ReleaseCounter
		{
			public int Comments { get; set; }
			public int Stars { get; set; }
			public int Downloads { get; set; }
		}

		public class ReleaseThumb : ReactiveObject
		{
			private Image _image;

			[DataMember]
			public Image Image { get { return _image; } set { this.RaiseAndSetIfChanged(ref _image, value); } }
			[DataMember]
			public Flavor Flavor { get; set; }
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
			Versions = release.Versions;
			Starred = release.Starred;
		}

		public override string ToString()
		{
			return $"[release] {Id} {Game.DisplayName} - {Name}";
		}
	}
}