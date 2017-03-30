using System;
using System.IO;
using System.Reactive.Linq;
using JetBrains.Annotations;
using ReactiveUI;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.PinballX.Models;

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
		/// Used to identify from any source.
		/// </remarks>
		public string FileId => _fileId.Value;

		/// <summary>
		/// Absolute file path to physical file or null if file doesn't exist
		/// </summary>
		public string FilePath { get { return _filePath; } set { this.RaiseAndSetIfChanged(ref _filePath, value); } }

		/// <summary>
		/// File size in bytes or 0 if file doesn't exist
		/// </summary>
		public long FileSize { get; set; }

		/// <summary>
		/// Linked game from PinballX's XML database
		/// </summary>
		public PinballXGame XmlGame { get { return _xmlGame; } set { this.RaiseAndSetIfChanged(ref _xmlGame, value); } }

		public string FileName => _fileName.Value;

		public bool Enabled => _enabled == null || _enabled.Value;

		// deps
		private readonly IFile _file;

		// watched props
		private string _filePath;
		private PinballXGame _xmlGame;

		// generated props
		private readonly ObservableAsPropertyHelper<bool> _enabled;
		private readonly ObservableAsPropertyHelper<string> _fileId;
		private ObservableAsPropertyHelper<string> _fileName;

		// status props
		public bool HasMapping => false;
		public bool HasLocalFile => FilePath != null;
		public bool HasXmlGame => XmlGame != null;

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
					this.WhenAnyValue(x => x.FilePath, x => x.XmlGame.Filename)
						.Select(x => x.Item1 != null ? Path.GetFileName(x.Item1) : x.Item2)
						.ToProperty(this, game => game.FileName, out _fileName);
				}
			});
		}

		public AggregatedGame([NotNull] PinballXGame xmlGame, IFile file) : this(file)
		{
			Update(xmlGame);

			// Enabled
			XmlGame.WhenAnyValue(g => g.Enabled)
				.Select(e => "true".Equals(e, StringComparison.InvariantCultureIgnoreCase))
				.ToProperty(this, game => game.Enabled, out _enabled);

			// FileId
			XmlGame.WhenAnyValue(g => g.System.TablePath, g => g.Filename)
				.Select(x => Path.Combine(x.Item1, x.Item2))
				.ToProperty(this, game => game.FileId, out _fileId);

			// Unlink local file when table path changes.
			XmlGame.WhenAnyValue(g => g.System.TablePath).Subscribe(newPath => ClearLocalFile());
		}

		public AggregatedGame([NotNull] string filePath, IFile file) : this(file)
		{
			// FileId
			this.WhenAnyValue(g => g.FilePath)
				.Select(filepath => Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath)))
				.ToProperty(this, game => game.FileId, out _fileId);

			Update(filePath);
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

		public void ClearXmlGame()
		{
			XmlGame = null;
		}

		public AggregatedGame Update(string filePath)
		{
			FilePath = filePath;
			FileSize = _file.FileSize(filePath);

			return this;
		}

		public void ClearLocalFile()
		{
			FilePath = null;
			FileSize = 0;
		}

		public bool EqualsXmlGame(PinballXGame xmlGame)
		{
			return XmlGame != null && XmlGame.Equals(xmlGame);
		}

		public bool EqualsFileId(string filePath)
		{
			return Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath)) == FileId;
		}
	}
}
