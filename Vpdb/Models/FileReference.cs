using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace VpdbAgent.Vpdb.Models
{
	public class FileReference
	{
		public string Id { get; set; }
		public string Name { get { return _name ?? Path.GetFileName(Url); } set { _name = value; } }
		public long Bytes { get; set; }
		public string MimeType { get; set; }
		public string FileType { get; set; }
		public Dictionary<string, FileVariation> Variations { get; set; }
		public FileCounter Counter { get; set; }
		public bool IsActive { get; set; }
		public bool IsProtected { get; set; }
		public string Url { get; set; }

		private string _name;

		public override string ToString()
		{
			return $"{Name} ({Id})";
		}
	}

	public class FileVariation
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

	public class FileCounter
	{
		public int Downloads { get; set; }

		public override string ToString()
		{
			return $"{Downloads} downloads";
		}
	}
}
