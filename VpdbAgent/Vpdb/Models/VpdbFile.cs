using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbFile
	{
		[BsonId] public string Id { get; set; }
		public string Name { get { return _name ?? Path.GetFileName(Url); } set { _name = value; } }
		public long Bytes { get; set; }
		public DateTime CreatedAt { get; set; }
		public string MimeType { get; set; }
		public string FileType { get; set; }
		public Dictionary<string, dynamic> Metadata { get; set; }
		public Dictionary<string, VpdbVariation> Variations { get; set; }
		public VpdbCounter Counter { get; set; }
		public bool IsActive { get; set; }
		public bool IsProtected { get; set; }
		public Uri Uri { get; private set; }

		[BsonIgnore]
		public string Url
		{
			get { return _url; }
			set
			{
				_url = value;
				Uri = new Uri(value);
			}
		}

		private string _name;
		private string _url;

		public void Update(VpdbFile file)
		{
			Name = file.Name;
			Bytes = file.Bytes;
			CreatedAt = file.CreatedAt;
			MimeType = file.MimeType;
			FileType = file.FileType;
			Metadata = file.Metadata;
			Variations = file.Variations;
			Counter = file.Counter;
			IsActive = file.IsActive;
			IsProtected = file.IsProtected;
			Url = file.Url;
		}

		public override string ToString()
		{
			return $"{Name} ({Id})";
		}
	}

	public class VpdbVariation
	{
		public string MimeType { get; set; }
		public long Bytes { get; set; }
		public int Height { get; set; }
		public int Width { get; set; }
		public string Url { get; set; }
		public bool IsProtected { get; set; }

		public override string ToString()
		{
			return Url;
		}
	}

	public class VpdbCounter
	{
		public int Downloads { get; set; }

		public override string ToString()
		{
			return $"{Downloads} downloads";
		}
	}
}
