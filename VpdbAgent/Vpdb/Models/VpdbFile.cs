using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using LiteDB;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbFile
	{
		[DataMember] [BsonId] public string Id { get; set; }
		[DataMember] public string Name { get { return _name ?? Path.GetFileName(Url); } set { _name = value; } }
		[DataMember] public long Bytes { get; set; }
		[DataMember] public DateTime CreatedAt { get; set; }
		[DataMember] public string MimeType { get; set; }
		[DataMember] public string FileType { get; set; }
		[DataMember] public Dictionary<string, dynamic> Metadata { get; set; }
		[DataMember] public Dictionary<string, VpdbVariation> Variations { get; set; }
		[DataMember] public VpdbCounter Counter { get; set; }
		[DataMember] public bool IsActive { get; set; }
		[DataMember] public bool IsProtected { get; set; }
		public Uri Uri { get; private set; }

		[DataMember] [BsonIgnore]
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
