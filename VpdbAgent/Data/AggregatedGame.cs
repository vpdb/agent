using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using JetBrains.Annotations;
using ReactiveUI;
using Splat;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Download;
using VpdbAgent.Vpdb.Models;
using ILogger = NLog.ILogger;

namespace VpdbAgent.Data
{
	/// <summary>
	/// A game, aggregated from the PinballX XML database, table file from the 
	/// disk and/or mappings to VPDB. It's what's shown in the UI of the app.
	/// </summary>
	/// 
	/// <remarks>
	/// This is how the merge works:
	/// 
	/// Whenever something updates, a list with updated objects is matched
	/// against every <see cref="AggregatedGame"/> by using one of the Equal
	/// overloads.
	/// 
	/// If there's a match, <see cref="AggregatedGame"/> is updated with one
	/// of the Update overloads.
	/// 
	/// If there's no match, a new <see cref="AggregatedGame"/> is instantiated
	/// using the respective constructor overload (which also calls the 
	/// corresponding Update overload).
	/// </remarks>
	public class AggregatedGame : ReactiveObject
	{
		/// <summary>
		/// Absolute file path without file extension.
		/// </summary>
		/// <remarks>
		/// Used to identify from any source. It is retrieved from:
		/// 
		///    - <see cref="PinballXGame.FileName"/> for <see cref="XmlGame"/>
		///    - <see cref="Data.Mapping.FileId"/> for <see cref="Mapping"/>
		///    - <see cref="FilePath"/> for local files
		/// 
		/// Since the file ID is based on file names, we need to define what
		/// happens if file names change.
		/// 
		/// - If the local file is renamed, the mapping is renamed as well,
		///   while the <see cref="XmlGame"/> is cleared and re-matched (see
		///   <see cref="GameManager.OnTableFileRenamed"/>).
		/// - If <see cref="PinballXGame.FileName"/> changes, the new entry is 
		///   re-matched and the old entry is removed.
		/// - If <see cref="Data.Mapping.FileId"/> changes, the new entry is
		///   also re-matched and old one discarded.
		/// 
		/// </remarks>
		public string FileId { get { return _fileId; } private set { this.RaiseAndSetIfChanged(ref _fileId, value); } }

		/// <summary>
		/// Absolute file path to physical file or null if file doesn't exist
		/// </summary>
		public string FilePath { get { return _filePath; } private set { this.RaiseAndSetIfChanged(ref _filePath, value); } }

		/// <summary>
		/// File size in bytes or 0 if file doesn't exist
		/// </summary>
		public long FileSize { get; private set; }

		/// <summary>
		/// Linked game from PinballX's XML database
		/// </summary>
		public PinballXGame XmlGame { get { return _xmlGame; } private set { this.RaiseAndSetIfChanged(ref _xmlGame, value); } }

		/// <summary>
		/// Linked release at VPDB
		/// </summary>
		public Mapping Mapping { get { return _mapping; } private set { this.RaiseAndSetIfChanged(ref _mapping, value); } }

		/// <summary>
		/// Mapped release data
		/// </summary>
		public VpdbRelease MappedRelease { get { return _mappedRelease; } private set { this.RaiseAndSetIfChanged(ref _mappedRelease, value); } }

		/// <summary>
		/// Mapped version data of the release
		/// </summary>
		public VpdbVersion MappedVersion { get { return _mappedVersion; } private set { this.RaiseAndSetIfChanged(ref _mappedVersion, value); } }

		/// <summary>
		/// Mapped file data of the release
		/// </summary>
		public VpdbTableFile MappedTableFile { get { return _mappedTableFile; } private set { this.RaiseAndSetIfChanged(ref _mappedTableFile, value); } }

		/// <summary>
		/// Mapped file data of the release
		/// </summary>
		public Job MappedJob => _mappedJob.Value;

		// convenient props
		public string Description => XmlGame != null ? XmlGame.Description : (MappedRelease != null ? $"{MappedRelease.Game.Title} ({MappedRelease.Game.Manufacturer} {MappedRelease.Game.Year})" : null);
		public string FileDisplayName => FileName ?? Mapping?.FileName;
		public string FileName => _fileName.Value;
		public bool IsVisible => _isVisible.Value;
		public PinballXSystem System => XmlGame?.System ?? Mapping?.System;
		
		// status props
		public bool HasMapping => _hasMapping.Value;
		public bool HasLocalFile => _hasLocalFile.Value;
		public bool HasXmlGame => _hasXmlGame.Value;
		public bool HasSystem => System != null;

		// watched props
		private string _fileId;
		private string _filePath;
		private PinballXGame _xmlGame;
		private Mapping _mapping;
		private VpdbRelease _mappedRelease;
		private VpdbVersion _mappedVersion;
		private VpdbTableFile _mappedTableFile;

		// generated props
		private readonly ObservableAsPropertyHelper<bool> _hasMapping;
		private readonly ObservableAsPropertyHelper<bool> _hasLocalFile;
		private readonly ObservableAsPropertyHelper<bool> _hasXmlGame;
		private ObservableAsPropertyHelper<bool> _isVisible;
		private ObservableAsPropertyHelper<string> _fileName;
		private ObservableAsPropertyHelper<Job> _mappedJob;

		// deps
		private readonly IFile _file;
		private readonly ILogger _logger;
		private readonly IVpdbManager _vpdbManager;
		private readonly IJobManager _jobManager;

		/// <summary>
		/// Base constructor
		/// </summary>
		/// <param name="resolver">Dependency resolver</param>
		private AggregatedGame(IDependencyResolver resolver)
		{
			_file = resolver.GetService<IFile>();
			_logger = resolver.GetService<ILogger>();
			_vpdbManager = resolver.GetService<IVpdbManager>();
			_jobManager = resolver.GetService<IJobManager>();

			// status props
			this.WhenAnyValue(x => x.Mapping).Select(x => x != null).ToProperty(this, g => g.HasMapping, out _hasMapping);
			this.WhenAnyValue(x => x.FilePath).Select(x => x != null).ToProperty(this, g => g.HasLocalFile, out _hasLocalFile);
			this.WhenAnyValue(x => x.XmlGame).Select(x => x != null).ToProperty(this, g => g.HasXmlGame, out _hasXmlGame);

			// FileName
			this.WhenAnyValue(x => x.XmlGame).Subscribe(xmlGame => {
				if (xmlGame == null) {
					this.WhenAnyValue(g => g.FilePath)
						.Select(Path.GetFileName)
						.ToProperty(this, game => game.FileName, out _fileName);
				} else {
					this.WhenAnyValue(x => x.FilePath, x => x.XmlGame.FileName)
						.Select(x => x.Item1 != null ? Path.GetFileName(x.Item1) : x.Item2)
						.ToProperty(this, game => game.FileName, out _fileName);
				}
			});

			// visibility
			this.WhenAnyValue(x => x.XmlGame, x => x.Mapping).Subscribe(x => {
				if (x.Item1 != null && x.Item2 != null) {
					this.WhenAnyValue(g => g.XmlGame.Enabled, g => g.Mapping.IsHidden)
						.Select(y => (y.Item1 == null || "true".Equals(y.Item1, StringComparison.InvariantCultureIgnoreCase)) && !y.Item2)
						.ToProperty(this, game => game.IsVisible, out _isVisible);

				} else if (x.Item1 == null && x.Item2 != null) {
					this.WhenAnyValue(g => g.Mapping.IsHidden)
						.Select(isHidden => !isHidden)
						.ToProperty(this, game => game.IsVisible, out _isVisible);

				} else if (x.Item1 != null && x.Item2 == null) {
					this.WhenAnyValue(g => g.XmlGame.Enabled)
						.Select(enabled => enabled == null || "true".Equals(enabled, StringComparison.InvariantCultureIgnoreCase))
						.ToProperty(this, game => game.IsVisible, out _isVisible);
				} else {
					Observable.Return(true).ToProperty(this, game => game.IsVisible, out _isVisible);
				}
			});

			// auto-update vpdb data when mapping changes
			this.WhenAnyValue(x => x.Mapping).Subscribe(mapping => {
				if (mapping == null) {
					MappedTableFile = null;
					MappedVersion = null;
					MappedRelease = null;

				} else {
					mapping
						.WhenAnyValue(m => m.FileId, m => m.ReleaseId)
						.Where(x => x.Item1 != null && x.Item2 != null)
						.SelectMany(x => _vpdbManager.GetRelease(mapping.ReleaseId))
						.Subscribe(x => SetRelease(x, mapping.FileId));
					
					mapping.WhenAnyValue(m => m.JobId)
						.Where(jobId => jobId != null)
						.Select(jobId => _jobManager.CurrentJobs.FirstOrDefault(job => job.Id == jobId))
						.ToProperty(this, game => game.MappedJob, out _mappedJob);
				}
			});

		}

		/// <summary>
		/// Constructs based on XML game.
		/// </summary>
		/// <param name="xmlGame">Parsed game from PinballX XML database</param>
		/// <param name="file">IFile dependency</param>
		public AggregatedGame([NotNull] PinballXGame xmlGame) : this(Locator.Current)
		{
			Update(xmlGame);
			FileId = Path.Combine(xmlGame.System.TablePath, xmlGame.FileName);

			// Unlink local file when table path changes.
			XmlGame.WhenAnyValue(g => g.System.TablePath).Subscribe(newPath => ClearLocalFile());
		}

		/// <summary>
		/// Constructs based on local file.
		/// </summary>
		/// <param name="filePath">Absolute path to file</param>
		/// <param name="file">IFile dependency</param>
		public AggregatedGame([NotNull] string filePath) : this(Locator.Current)
		{
			Update(filePath);
		}

		/// <summary>
		/// Constructs based on Mapping.
		/// </summary>
		/// <param name="mapping">Parsed Mapping</param>
		/// <param name="file">IFile dependency</param>
		public AggregatedGame([NotNull] Mapping mapping) : this(Locator.Current)
		{
			Update(mapping);
			FileId = mapping.Id;
		}

		/// <summary>
		/// Renames the file ID of the game.
		/// </summary>
		/// 
		/// <remarks>
		/// This happens when a local file with a mapping has been renamed. In 
		/// this case we want to keep the mapping linked to the game. The 
		/// mapping is renamed as well.
		/// </remarks>
		/// 
		/// <param name="filePath">Absolute path to new file name</param>
		/// <param name="newXmlGame">If set, link to this XML game. This means that an entry with the new file name exists already and its mapping will be overwritten existing.</param>
		public void Rename(string filePath, PinballXGame newXmlGame = null)
		{
			if (!HasMapping || !HasLocalFile) {
				throw new InvalidOperationException("Can only rename games with mapping and local file.");
			}
			Update(filePath);
			if (newXmlGame != null) {
				XmlGame = newXmlGame;
			}
			Mapping.Rename(filePath);
		}

		/// <summary>
		/// Replaces current mapping with a new one to the given VPDB file and also updates
		/// local file data with new path.
		/// </summary>
		/// <param name="file">File to remap to</param>
		/// <param name="filePath">New local file path</param>
		public void Remap(VpdbFile file, string filePath) {

			// update local file
			Update(filePath);

			// update mapping
			var previousFileId = Mapping.FileId;
			Mapping = new Mapping(this, MappedRelease, file.Id) {
				PreviousFileId = previousFileId
			};
		}

		/// <summary>
		/// Updates XML game.
		/// </summary>
		/// <param name="xmlGame">Parsed game from PinballX XML database</param>
		/// <returns>This instance</returns>
		public AggregatedGame Update(PinballXGame xmlGame)
		{
			if (XmlGame == null) {
				XmlGame = xmlGame;
			} else {
				XmlGame.Update(xmlGame);
			}
			return this;
		}

		/// <summary>
		/// Removes the relation to the PinballX XML database.
		/// </summary>
		public void ClearXmlGame()
		{
			XmlGame = null;
		}

		/// <summary>
		/// Updates local file data.
		/// </summary>
		/// <param name="filePath">Absolute path to local file</param>
		/// <returns>This instance</returns>
		public AggregatedGame Update(string filePath)
		{
			FileId = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath));
			FilePath = filePath;
			FileSize = _file.FileSize(filePath);

			return this;
		}

		/// <summary>
		/// Removes the relation to the local file.
		/// </summary>
		public void ClearLocalFile()
		{
			FilePath = null;
			FileSize = 0;
		}

		/// <summary>
		/// Updates or creates the Mapping.
		/// </summary>
		/// <param name="mapping">New mapping</param>
		/// <returns>This instance</returns>
		public AggregatedGame Update(Mapping mapping)
		{
			if (Mapping == null) {
				Mapping = mapping;
			} else {
				Mapping.Update(mapping);
			}
			return this;
		}

		/// <summary>
		/// Removes the mapping.
		/// </summary>
		public void ClearMapping()
		{
			Mapping = null;
		}

		/// <summary>
		/// Checks if the XML game is equal, i.e. the data is equal (to 
		/// check if it should be updated).
		/// </summary>
		/// <param name="xmlGame">XML game to compare</param>
		/// <returns>True if equal, false otherwise</returns>
		public bool EqualsXmlGame(PinballXGame xmlGame)
		{
			return XmlGame != null && XmlGame.Equals(xmlGame);
		}

		/// <summary>
		/// Checks if the local file is the equal.
		/// </summary>
		/// <param name="filePath">Absolute path to local file</param>
		/// <returns>True if equal, false otherwise</returns>
		public bool EqualsFileId(string filePath)
		{
			return Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath)) == FileId;
		}

		/// <summary>
		/// Checks if the Mapping is equal, i.e. the data is equal (to check
		/// if it should be updated).
		/// </summary>
		/// <param name="mapping">Mapping to compare</param>
		/// <returns>True if equal, false otherwise</returns>
		public bool EqualsMapping(Mapping mapping)
		{
			return Mapping != null && Mapping.Equals(mapping);
		}

		/// <summary>
		/// Links a download job to a game.
		/// </summary>
		/// <param name="job">Job to link to this gamed</param>
		public void SetJob(Job job)
		{
			if (Mapping != null) {
				Mapping.JobId = job.Id;
			} else {
				_logger.Warn("Cannot assign job to game without mapping. ");
			}
		}

		/// <summary>
		/// Sets VPDB release data when mapping data is available.
		/// </summary>
		/// <param name="release"></param>
		/// <param name="fileId"></param>
		private void SetRelease(VpdbRelease release, string fileId)
		{
			MappedRelease = release;
			MappedVersion = release.GetVersion(fileId);
			MappedTableFile = release.GetFile(fileId);
		}
	}
}
