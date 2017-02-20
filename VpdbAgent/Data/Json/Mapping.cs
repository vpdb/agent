using System;
using System.IO;
using System.Reactive;
using System.Runtime.Serialization;
using ReactiveUI;
using Splat;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.Data.Objects;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Data.Json
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
	public class Mapping : ReactiveObject, IComparable<Mapping>
	{
		/// <summary>
		/// "ID" of the game within PinballX's platform. 
		/// Comes from the <c>&lt;description&gt;</c> tag of the XML, usually something
		/// like: "Theatre of Magic (Midway 1995)"
		/// </summary>
		/// <remarks>
		/// Maps to <see cref="PinballXGame.Description"/>.
		/// </remarks>
		[DataMember] public string Id { get; set; }

		/// <summary>
		/// The entire filename with extension but without path if exists. If file was
		/// not found, contains only filename without extensions.
		/// </summary>
		/// <remarks>
		/// Maps to <see cref="PinballXGame.Filename"/>.
		/// </remarks>
		[DataMember] public string Filename { get; set; }

		/// <summary>
		/// Entire filename of the XML where the game was defined, without
		/// path.
		/// </summary>
		[DataMember] public string DatabaseFile { get; set; }

		/// <summary>
		/// True if enabled in PinballX, false otherwise.
		/// </summary>
		[DataMember] public bool Enabled { get; set; } = true;

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
		[DataMember]
		public string PreviousFileIds { get { return _previousFileId; } set { this.RaiseAndSetIfChanged(ref _previousFileId, value); } }

		/// <summary>
		/// The table script as it was saved back after patching. If null, the script
		/// either hasn't previously been updated, there was no previous version
		/// or patching resulted in a conflict.
		/// </summary>
		[DataMember] public string PatchedTableScript { get { return _patchedTableScript; } set { this.RaiseAndSetIfChanged(ref _patchedTableScript, value); } }

		// read/write fields
		private string _releaseId;
		private bool _exists;
		private string _fileId;
		private bool _isSynced;
		private string _patchedTableScript;
		private string _previousFileId;

		// non-serialized props
		public bool Exists { get { return _exists; } set { this.RaiseAndSetIfChanged(ref _exists, value); } }
		public long FileSize { get; set; }
		public Platform Platform { get; private set; }


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
		public Mapping(IDependencyResolver resolver)
		{
			_file = resolver.GetService<IFile>();
		}
		
		/// <summary>
		/// Constructor when creating from a PinballX game (from XML)
		/// </summary>
		/// <param name="xmlGame">PinballX Game</param>
		/// <param name="platform">PinballX System</param>
		/// <param name="dependencyResolver">Resolver</param>
		public Mapping(PinballXGame xmlGame, Platform platform, IDependencyResolver dependencyResolver) : this(dependencyResolver)
		{
			Update(platform);
			UpdateFromGame(xmlGame, platform.TablePath);
		}

		/// <summary>
		/// Updates data from the PinballX game with the same ID (description).
		/// </summary>
		/// <param name="xmlGame"></param>
		/// <param name="platform"></param>
		/// <returns></returns>
		public Mapping Update(PinballXGame xmlGame, Platform platform)
		{
			Update(platform);
			UpdateFromGame(xmlGame, platform.TablePath);
			return this;
		}

		/// <summary>
		/// Updates platform and subscribes to attribute changes.
		/// </summary>
		/// <param name="platform"></param>
		private void Update(Platform platform)
		{
			Platform = platform;

			// save to disk if these attributes change
			this.WhenAny(m => m.ReleaseId, m => m.FileId, m => m.IsSynced, m => m.PreviousFileIds, m => m.PatchedTableScript, (rid, fid, s, pfid, script) => Unit.Default)
				.Subscribe(Platform.MappingPropertyChanged);
		}

		/// <summary>
		/// Updates <see cref="Id"/>, <see cref="Enabled"/>, <see cref="DatabaseFile"/>
		/// based on the game's attributes, and <see cref="Filename"/>, 
		/// <see cref="FileSize"/> and <see cref="Exists"/> based on file 
		/// on disk.
		/// </summary>
		/// <param name="xmlGame"></param>
		/// <param name="tablePath"></param>
		private void UpdateFromGame(PinballXGame xmlGame, string tablePath)
		{
			Id = xmlGame.Description;
			Enabled = xmlGame.Enabled == null || "true".Equals(xmlGame.Enabled, StringComparison.InvariantCultureIgnoreCase);
			DatabaseFile = xmlGame.DatabaseFile;

			var oldFilename = Filename;

			// check if exists
			var vptPath = Path.Combine(tablePath, xmlGame.Filename + ".vpt");
			var vpxPath = Path.Combine(tablePath, xmlGame.Filename + ".vpx");
			if (_file.Exists(vptPath)) {
				Filename = Path.GetFileName(vptPath);
				FileSize = _file.FileSize(vptPath);
				Exists = true;
			} else if (_file.Exists(vpxPath)) {
				Filename = Path.GetFileName(vpxPath);
				FileSize = _file.FileSize(vpxPath);
				Exists = true;
			} else {
				Filename = xmlGame.Filename;
				Exists = false;
			}

			// if filename has changed, unlink.
			if (oldFilename != Filename) {
				FileId = null;
				ReleaseId = null;
			}
		}

		public bool Equals(Mapping mapping)
		{
			return mapping?.Id != null && mapping.Id.Equals(Id);
		}

		public bool Equals(PinballXGame game)
		{
			return game?.Description != null && game.Description.Equals(Id);
		}

		public int CompareTo(Mapping other)
		{
			return other == null ? 1 : string.Compare(Id, other.Id, StringComparison.Ordinal);
		}

		public override string ToString()
		{
			return Id;
		}
	}
}
