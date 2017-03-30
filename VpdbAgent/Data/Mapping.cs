using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ReactiveUI;
using Splat;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Data
{
	/// <summary>
	/// Maps data to VPDB. This is the root node.
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
	///       "filename": "Theatre of magic VPX NZ-TT 1.0.vpx",
	///       "release_id": "e2wm7hdp9b",
	///       "file_id": "skkj298nr8",
	///       "is_synced": false
	///     }
	///   ]
	/// }
	/// </code>
	/// 
	/// This class is used to serialize the above data. Note that for every
	/// system from PinballX, a separate instance of this class is created.
	/// </remarks>
	public class SystemMapping
	{
		/// <summary>
		/// List of mappings
		/// </summary>
		[DataMember] public IEnumerable<Mapping> Mappings { set; get; }

		public SystemMapping()
		{
			Mappings = new List<Mapping>();
		}

		public SystemMapping(IEnumerable<Mapping> games)
		{
			Mappings = games;
		}

		public override string ToString()
		{
			return $"[SystemMapping] {Mappings.Count()} mapping(s)";
		}
	}

	/// <summary>
	/// Maps a local file to VPDB.
	/// </summary>
	/// <remarks>
	/// It's also possible to use a Mapping for ignoring local unimportant files.
	/// In this case, the mapping doesn't contain `id` or VPDB IDs, just 
	/// `filename` with an `is_hidden` property.
	/// 
	/// Every Mapping creates an entry in Global Games. If an entry for the given
	/// ID or filename already exists, it is updated and enriched with data from 
	/// VPDB. It's possible that a game is listed in VPDB Agent that has neither
	/// a local file nor a PinballX database entry if that game was previously 
	/// mapped to a local file which has been removed since.
	/// </remarks>
	public class Mapping : ReactiveObject
	{
		/// <summary>
		/// The entire filename with extension but without path.
		/// </summary>
		[DataMember] public string Filename { get; set; }

		/// <summary>
		/// Release ID of the linked release at VPDB.
		/// </summary>
		[DataMember] public string ReleaseId { get { return _releaseId; } set { this.RaiseAndSetIfChanged(ref _releaseId, value); } }

		/// <summary>
		/// File ID of the linked file at VPDB.
		/// </summary>
		[DataMember] public string FileId { get { return _fileId; } set { this.RaiseAndSetIfChanged(ref _fileId, value); } }

		/// <summary>
		/// True if should be updated automatically, false otherwise.
		/// </summary>
		[DataMember] public bool IsSynced { get { return _isSynced; } set { this.RaiseAndSetIfChanged(ref _isSynced, value); } }

		/// <summary>
		/// File ID of the linked file at VPDB.
		/// </summary>
		[DataMember] public string PreviousFileIds { get { return _previousFileId; } set { this.RaiseAndSetIfChanged(ref _previousFileId, value); } }

		/// <summary>
		/// The table script as it was saved back after patching. If null, the script
		/// either hasn't previously been updated, there was no previous version
		/// or patching resulted in a conflict.
		/// </summary>
		[DataMember] public string PatchedTableScript { get { return _patchedTableScript; } set { this.RaiseAndSetIfChanged(ref _patchedTableScript, value); } }

		// non-serialized props
		public PinballXSystem System { get; set; }

		// read/write fields
		private string _releaseId;
		private string _fileId;
		private bool _isSynced;
		private string _previousFileId;
		private string _patchedTableScript;

		// dependencies
		private readonly IFile _file;

		/// <summary>
		/// Constructor when unmarshalling from file
		/// </summary>
		public Mapping() : this(Locator.Current)
		{
		}

		/// <summary>
		/// Base constructor
		/// </summary>
		/// <param name="resolver">Resolver</param>
		private Mapping(IDependencyResolver resolver)
		{
			_file = resolver.GetService<IFile>();
		}
	}

}
