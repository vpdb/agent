using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb.Download
{
	public enum FileType
	{
		/// <summary>
		/// The vpt/vpx file of a release that goes into the `Tables` folder
		/// </summary>
		TableFile,

		/// <summary>
		/// A script file that goes into the `Scripts` folder
		/// </summary>
		TableScript,

		/// <summary>
		/// Any other table file that goes into the `Tables` folder
		/// </summary>
		TableAuxiliary,

		/// <summary>
		/// An MP3 that goes into the `Music` folder
		/// </summary>
		TableMusic,

		/// <summary>
		/// A table screenshot
		/// </summary>
		TableImage,

		/// <summary>
		/// A table video
		/// </summary>
		TableVideo,

		/// <summary>
		/// A backglass screenshot
		/// </summary>
		BackglassImage,

		/// <summary>
		/// A game logo (wheel image)
		/// </summary>
		WheelImage
	}
}
