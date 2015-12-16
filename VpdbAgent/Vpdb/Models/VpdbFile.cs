using System;
using System.Collections.Generic;
using System.IO;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbFile
	{
		public string Id { get; set; }
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
		public string Url { get; set; }

		private string _name;

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
