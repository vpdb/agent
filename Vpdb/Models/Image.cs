using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Models
{
	public class Image
	{
		public string Url { get; set; }
		public bool IsProtected { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
	}
}
