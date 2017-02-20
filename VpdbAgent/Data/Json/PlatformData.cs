using System.Collections.Generic;
using System.Linq;

namespace VpdbAgent.Data.Json
{
	/// <summary>
	/// Maps local files of a platform to VPDB.
	/// </summary>
	/// 
	/// <remarks>
	/// When linking local data to VPDB, we need a way of persisting that 
	/// mapping in a transparent way. For that, we create a `vpdb.json` file
	/// in every system folder. Such a file looks something like that:
	/// <code>
	/// {
	///   "mappings": [
	///     {
	///       "id": "Theatre of Magic (Bally 1995)",
	///       "filename": "Theatre of magic VPX NZ-TT 1.0.vpx",
	///       "database_file": "Visual Pinball.xml",
	///       "enabled": true,
	///       "release_id": "e2wm7hdp9b",
	///       "file_id": "skkj298nr8",
	///       "is_synced": false
	///     }
	///   ]
	/// }
	/// </code>
	/// 
	/// This class is used to serialize the above data. Note that for every
	/// System at PinballX, a separate instance of this class is created.
	/// </remarks>
	public class PlatformData
	{
		/// <summary>
		/// List of mappings
		/// </summary>
		public IEnumerable<Mapping> Mappings { set; get; }

		public PlatformData()
		{
			Mappings = new List<Mapping>();
		}

		public PlatformData(IEnumerable<Mapping> games)
		{
			Mappings = games;
		}

		public override string ToString()
		{
			return $"[PlatformDB] {Mappings.Count()} mapping(s)";
		}
	}
}
