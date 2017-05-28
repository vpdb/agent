using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using ReactiveUI;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Download;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Data
{
	/// <summary>
	/// Maps a local file to VPDB.
	/// </summary>
	/// 
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
	/// 
	/// Mappings are self-saving, i.e. updating a mapping or adding/removing
	/// mappings will persist them automatically to the disk.
	/// </remarks>
	public class Mapping : ReactiveObject
	{
		/// <summary>
		/// Mapping ID. Equals <see cref="AggregatedGame.FileId"/>.
		/// </summary>
		public string Id => Path.Combine(System.TablePath, Path.GetFileNameWithoutExtension(FileName));

		/// <summary>
		/// The entire filename with extension but without path.
		/// </summary>
		[DataMember] public string FileName { get { return _fileName; } private set { this.RaiseAndSetIfChanged(ref _fileName, value); } }

		/// <summary>
		/// Release ID of the linked release at VPDB.
		/// </summary>
		[DataMember] public string ReleaseId { get { return _releaseId; } private set { this.RaiseAndSetIfChanged(ref _releaseId, value); } }

		/// <summary>
		/// File ID of the linked file at VPDB.
		/// </summary>
		[DataMember] public string FileId { get { return _fileId; } private set { this.RaiseAndSetIfChanged(ref _fileId, value); } }

		/// <summary>
		/// Job ID. We also link jobs to games so we can display download status.
		/// </summary>
		[DataMember] public int? JobId { get { return _jobId; } set { this.RaiseAndSetIfChanged(ref _jobId, value); } }

		/// <summary>
		/// True if should be updated automatically, false otherwise.
		/// </summary>
		[DataMember] public bool IsSynced { get { return _isSynced; } set { this.RaiseAndSetIfChanged(ref _isSynced, value); } }

		/// <summary>
		/// True if should be hidden in UI, false otherwise.
		/// </summary>
		[DataMember] public bool IsHidden { get { return _isHidden; } set { this.RaiseAndSetIfChanged(ref _isHidden, value); } }

		/// <summary>
		/// File ID of the linked file at VPDB.
		/// </summary>
		[DataMember] public string PreviousFileId { get { return _previousFileId; } set { this.RaiseAndSetIfChanged(ref _previousFileId, value); } }

		/// <summary>
		/// The table script as it was saved back after patching. If null, the script
		/// either hasn't previously been updated, there was no previous version
		/// or patching resulted in a conflict.
		/// </summary>
		[DataMember] public string PatchedTableScript { get { return _patchedTableScript; } set { this.RaiseAndSetIfChanged(ref _patchedTableScript, value); } }

		/// <summary>
		/// System from which the mapping is parsed from. We can't make it read-only 
		/// because instances are created by the serializator, but it's always set.
		/// </summary>
		public PinballXSystem System { get; set; }

		/// <summary>
		/// Stale means that the mapping is of no use anymore, i.e. no more linked release and no non-default flags.
		/// </summary>
		public bool IsStale => _fileId == null && !_isHidden;

		// read/write fields
		private string _fileName;
		private string _releaseId;
		private string _fileId;
		private int? _jobId;
		private bool _isSynced;
		private bool _isHidden;
		private string _previousFileId;
		private string _patchedTableScript;

		/// <summary>
		/// Constructor when unmarshalling from file
		/// </summary>
		public Mapping()
		{
		}

		/// <summary>
		/// Constructor with given game
		/// </summary>
		/// <param name="game">Game to which the mapping is linked to</param>
		/// <param name="system">System to which the game is linked to</param>
		public Mapping(AggregatedGame game, PinballXSystem system)
		{
			System = system;
			FileName = Path.GetFileName(game.FilePath);
		}

		/// <summary>
		/// Constructor when linking game to VPDB
		/// </summary>
		/// <param name="game">Game to link to</param>
		/// <param name="system">System to link to</param>
		/// <param name="release">VPDB release</param>
		/// <param name="fileId">File ID of VPDB release</param>
		public Mapping(AggregatedGame game, PinballXSystem system, VpdbRelease release, string fileId) : this(game, system)
		{
			ReleaseId = release.Id;
			FileId = fileId;
		}

		/// <summary>
		/// Copies values from another mapping to this mapping.
		/// </summary>
		/// <param name="mapping">Source mapping</param>
		public void Update(Mapping mapping)
		{
			FileName = mapping.FileName;
			ReleaseId = mapping.ReleaseId;
			FileId = mapping.FileId;
			JobId = mapping.JobId;
			IsSynced = mapping.IsSynced;
			IsHidden = mapping.IsHidden;
			PreviousFileId = mapping.PreviousFileId;
			PatchedTableScript = mapping.PatchedTableScript;
		}

		/// <summary>
		/// Maps this mapping to a VPDB release.
		/// </summary>
		/// <param name="release">Release to map to</param>
		/// <param name="fileId">File ID of the release corresponding to the mapping</param>
		/// <returns>This mapping</returns>
		public Mapping Map(VpdbRelease release, string fileId)
		{
			ReleaseId = release.Id;
			FileId = fileId;

			return this;
		}

		/// <summary>
		/// Renames this mapping.
		/// </summary>
		/// 
		/// <remarks>
		/// Changes <see cref="Id"/> and <see cref="FileName"/>.
		/// </remarks>
		/// <param name="filePath"></param>
		public void Rename(string filePath)
		{
			FileName = Path.GetFileName(filePath);
		}

		/// <summary>
		/// Links a job to this mapping.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that it will be unlinked when download finishes or fails.
		/// </remarks>
		/// <param name="job">Job to link</param>
		public void Link(Job job)
		{
			JobId = job.Id;
			job.WhenAnyValue(j => j.Status).Subscribe(status => {
				if (status == Job.JobStatus.Aborted || status == Job.JobStatus.Completed || status == Job.JobStatus.Failed) {
					JobId = null;
				}
			});
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) {
				return false;
			}
			if (ReferenceEquals(this, obj)) {
				return true;
			}
			var mapping = obj as Mapping;
			if (mapping == null) {
				return false;
			}

			return 
				FileName == mapping.FileName &&
				ReleaseId == mapping.ReleaseId &&
				FileId == mapping.FileId &&
				JobId == mapping.JobId &&
				IsSynced == mapping.IsSynced &&
				IsHidden == mapping.IsHidden &&
				PreviousFileId == mapping.PreviousFileId &&
				PatchedTableScript == mapping.PatchedTableScript;
		}
		
		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return FileName.GetHashCode();
		}
	}

}
