using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Data.Objects
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
		public string FileId { get; private set; }

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

		// deps
		private readonly IFile _file;

		// watched props
		private string _filePath;
		private PinballXGame _xmlGame;

		// status props
		public bool HasMapping => false;
		public bool HasLocalFile => FilePath != null;
		public bool HasXmlGame => XmlGame != null;

		// convenient props
		public string FileName => Path.GetFileName(FilePath);

		/// <summary>
		/// Base constructor
		/// </summary>
		/// <param name="file">IFile dependency</param>
		private AggregatedGame(IFile file)
		{
			_file = file;
		}

		public AggregatedGame(PinballXGame game, IFile file) : this(file)
		{
			Update(game);
		}

		public AggregatedGame(string filePath, IFile file) : this(file)
		{
			FileId = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath));
			Update(filePath);
		}

		public AggregatedGame Update(PinballXGame xmlGame)
		{
			FileId = Path.Combine(xmlGame.System.TablePath, xmlGame.Filename);

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
