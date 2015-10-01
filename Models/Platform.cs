using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Models
{
	public class Platform : ReactiveObject
	{
		/// <summary>
		/// Name of the platform. Serves as ID.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// True if enabled in PinballX.ini, False otherwise.
		/// </summary>
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// True if selected in UI, False otherwise.
		/// </summary>
		public bool IsSelected { get; set; } = true;

		/// <summary>
		/// The working path of the executable when launched
		/// </summary>
		public string WorkingPath { get; set; }

		/// <summary>
		/// Folder of the platform's table files
		/// </summary>
		public string TablePath { get; set; }

		/// <summary>
		/// File name of the executable
		/// </summary>
		public string Executable { get; set; }

		/// <summary>
		/// Parameters for the executable to play the table.
		/// </summary>
		/// <example>
		/// /play -"[TABLEPATH]\[TABLEFILE]"
		/// </example>
		public string Parameters { get; set; }

		/// <summary>
		/// Platform type. 
		/// </summary>
		public PlatformType Type { get; set; }

		/// <summary>
		/// Absolute path to database folder.
		/// </summary>
		public string DatabasePath { get; set; }

		/// <summary>
		/// Absolute path to media folder.
		/// </summary>
		public string MediaPath { get; set; }

		/// <summary>
		/// Absolute path to our internal database JSON file.
		/// </summary>
		public string DatabaseFile => DatabasePath + @"\vpdb.json";

		public Platform()
		{
		}

		public Platform(PinballXSystem system)
		{
			Name = system.Name;
			IsEnabled = system.Enabled;
			WorkingPath = system.WorkingPath;
			TablePath = system.TablePath;
			Executable = system.Executable;
			Parameters = system.Parameters;
			Type = system.Type;
			DatabasePath = system.DatabasePath;
			MediaPath = system.MediaPath;
		}

		public enum PlatformType
		{
			VP, FP, Custom
		}
	}
}
