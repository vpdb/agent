using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using ReactiveUI;
using Splat;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Data
{
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
		[DataMember] public string PreviousFileIds { get { return _previousFileId; } set { this.RaiseAndSetIfChanged(ref _previousFileId, value); } }

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

		// read/write fields
		private string _fileName;
		private string _releaseId;
		private string _fileId;
		private bool _isSynced;
		private bool _isHidden;
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

		/// <summary>
		/// Constructor with given game
		/// </summary>
		/// <param name="game">Game to which the mapping is linked to</param>
		public Mapping(AggregatedGame game)
		{
			System = game.System;
			FileName = Path.GetFileName(game.FilePath);
		}

		/// <summary>
		/// Constructor when linking game to VPDB
		/// </summary>
		/// <param name="game">Game to link to</param>
		/// <param name="release">VPDB release</param>
		/// <param name="fileId">File ID of VPDB release</param>
		public Mapping(AggregatedGame game, VpdbRelease release, string fileId) : this(game)
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
			IsSynced = mapping.IsSynced;
			IsHidden = mapping.IsHidden;
			PreviousFileIds = mapping.PreviousFileIds;
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
				IsSynced == mapping.IsSynced &&
				IsHidden == mapping.IsHidden &&
				PreviousFileIds == mapping.PreviousFileIds &&
				PatchedTableScript == mapping.PatchedTableScript;
		}
		
		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return Id.GetHashCode();
		}
	}

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
		[DataMember] public IReactiveList<Mapping> Mappings { set; get; }

		public PinballXSystem System { set { Mappings.ToList().ForEach(m => m.System = value); } }

		/// <summary>
		/// Constructor when instantiating self-saving object
		/// </summary>
		/// <param name="path">Path to save</param>
		/// <param name="marshallManager">Marshaller dependency</param>
		public SystemMapping(string path, IMarshallManager marshallManager)
		{
			var mappings = new ReactiveList<Mapping> { ChangeTrackingEnabled = true };
			Observable.Merge(
				mappings.ItemChanged.Select(x => Unit.Default), 
				mappings.ItemsAdded.Select(x => Unit.Default),
				mappings.ItemsRemoved.Select(x => Unit.Default)
			).Sample(TimeSpan.FromSeconds(1)).Subscribe(x => Save(path, marshallManager));
			Mappings = mappings;
		}

		/// <summary>
		/// Default constructor when serializing
		/// </summary>
		public SystemMapping()
		{
			Mappings = new ReactiveList<Mapping>();
		}

		private void Save(string path, IMarshallManager marshallManager)
		{
			marshallManager.MarshallMappings(this, path);
		}

		public override string ToString()
		{
			return $"[SystemMapping] {Mappings.Count()} mapping(s)";
		}
	}

}
