using System;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using JetBrains.Annotations;
using ReactiveUI;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Data
{
	/// <summary>
	/// A game, aggregated from the PinballX XML database, table file from the 
	/// disk and mappings to VPDB. It's what's shown in the UI of the app.
	/// </summary>
	/// 
	/// <remarks>
	/// This is how the merge works:
	/// Whenever something updates, a list with updated objects is matched
	/// against every <see cref="AggregatedGame"/> by using one of the Equal
	/// overloads.
	/// If there's a match, <see cref="AggregatedGame"/> is updated with one
	/// of the Update overloads.
	/// If there's no match, a new <see cref="AggregatedGame"/> is instantiated
	/// using the respective constructor overload (which also calls the 
	/// corresponding Update overload).
	/// 
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
		///    - <see cref="FileName"/> for local files
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

		public VpdbRelease MappedRelease { get { return _mappedRelease; } private set { this.RaiseAndSetIfChanged(ref _mappedRelease, value); } }
		public VpdbVersion MappedVersion { get { return _mappedVersion; } private set { this.RaiseAndSetIfChanged(ref _mappedVersion, value); } }
		public VpdbFile MappedFile { get { return _mappedFile; } private set { this.RaiseAndSetIfChanged(ref _mappedFile, value); } }

		// convenient props
		public string FileName => _fileName.Value;
		public bool Visible => _visible.Value;
		public PinballXSystem System => XmlGame?.System ?? Mapping?.System;
		
		// status props
		public bool HasMapping => Mapping != null;
		public bool HasLocalFile => FilePath != null;
		public bool HasXmlGame => XmlGame != null;
		public bool HasSystem => System != null;
		public bool HasRelease => MappedFile != null;

		// deps
		private readonly IFile _file;

		// watched props
		private string _fileId;
		private string _filePath;
		private PinballXGame _xmlGame;
		private Mapping _mapping;
		private VpdbRelease _mappedRelease;
		private VpdbVersion _mappedVersion;
		private VpdbFile _mappedFile;

		// generated props
		private ObservableAsPropertyHelper<bool> _visible;
		private ObservableAsPropertyHelper<string> _fileName;

		/// <summary>
		/// Base constructor
		/// </summary>
		/// <param name="file">IFile dependency</param>
		private AggregatedGame(IFile file)
		{
			_file = file;

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
						.ToProperty(this, game => game.Visible, out _visible);

				} else if (x.Item1 == null && x.Item2 != null) {
					this.WhenAnyValue(g => g.Mapping.IsHidden)
						.Select(isHidden => !isHidden)
						.ToProperty(this, game => game.Visible, out _visible);

				} else if (x.Item1 != null && x.Item2 == null) {
					this.WhenAnyValue(g => g.XmlGame.Enabled)
						.Select(enabled => enabled == null || "true".Equals(enabled, StringComparison.InvariantCultureIgnoreCase))
						.ToProperty(this, game => game.Visible, out _visible);
				} else {
					Observable.Return(true).ToProperty(this, game => game.Visible, out _visible);
				}
			});

		}

		public AggregatedGame([NotNull] PinballXGame xmlGame, IFile file) : this(file)
		{
			Update(xmlGame);
			FileId = Path.Combine(xmlGame.System.TablePath, xmlGame.FileName);

			// Unlink local file when table path changes.
			XmlGame.WhenAnyValue(g => g.System.TablePath).Subscribe(newPath => ClearLocalFile());
		}

		public AggregatedGame([NotNull] string filePath, IFile file) : this(file)
		{
			Update(filePath);
		}

		public AggregatedGame([NotNull] Mapping mapping, IFile file) : this(file)
		{
			Update(mapping);
			FileId = mapping.FileId;
		}

		public AggregatedGame Update(PinballXGame xmlGame)
		{
			if (XmlGame == null) {
				XmlGame = xmlGame;
			} else {
				XmlGame.Update(xmlGame);
			}
			return this;
		}

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


		public void ClearXmlGame()
		{
			XmlGame = null;
		}

		public AggregatedGame Update(string filePath)
		{
			FileId = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath));
			FilePath = filePath;
			FileSize = _file.FileSize(filePath);

			return this;
		}

		public void ClearLocalFile()
		{
			FilePath = null;
			FileSize = 0;
		}

		public AggregatedGame Update(Mapping mapping)
		{
			if (Mapping == null) {
				Mapping = mapping;
			} else {
				Mapping.Update(mapping);
			}
			return this;
		}

		public void ClearMapping()
		{
			Mapping = null;
		}

		public bool EqualsXmlGame(PinballXGame xmlGame)
		{
			return XmlGame != null && XmlGame.Equals(xmlGame);
		}

		public bool EqualsFileId(string filePath)
		{
			return Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath)) == FileId;
		}

		public bool EqualsMapping(Mapping mapping)
		{
			return Mapping != null && Mapping.Equals(mapping);
		}

		public void SetRelease(VpdbRelease release, string fileId)
		{
			MappedRelease = release;
			MappedVersion = release.GetVersion(fileId);
			MappedFile = release.GetFile(fileId);
		}
	}
}
