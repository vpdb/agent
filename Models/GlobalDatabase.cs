using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Models
{
	/// <summary>
	/// The platform-independent database that is saved as .json in PinballX's
	/// database folder.
	/// </summary>
	public class GlobalDatabase
	{
		/// <summary>
		/// A dictionary with releases from VPDB. These serve as cache and are
		/// updated at every application start as well as during runtime through
		/// push messages.
		/// </summary>
		public Dictionary<string, Release> Releases { set; get; } = new Dictionary<string, Release>();

		public override string ToString()
		{
			return $"[GlobalDB] {Releases.Count()} release(s)";
		}
	}
}
