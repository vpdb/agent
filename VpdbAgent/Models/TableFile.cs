using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Models
{
	public class TableFile
	{
		/// <summary>
		/// "ID" of the game within PinballX's platform. 
		/// Comes from the <c>&lt;description&gt;</c> tag of the XML, usually 
		/// something like: "Theatre of Magic (Midway 1995)". This is also how
		/// the media is named.
		/// </summary>
		/// <remarks>
		/// Maps to <see cref="PinballXGame.Description"/>. If null, there is
		/// no entry of it in any database file.
		/// </remarks>
		[CanBeNull] public string PinballXId { get; set; }

		/// <summary>
		/// The entire filename with extension but without path if exists. If file was
		/// not found, contains only filename without extensions.
		/// </summary>
		/// <remarks>
		/// Maps to <see cref="PinballXGame.Filename"/>.
		/// </remarks>
		[NotNull] public string Filename { get; set; }

		/// <summary>
		/// Absolute path to the file or null if not found.
		/// </summary>
		[CanBeNull] public string Path { get; set; }


	}
}
