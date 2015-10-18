using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VpdbAgent.Common;
using VpdbAgent.PinballX.Models;
using System.Reactive.Linq;

namespace VpdbAgent.PinballX
{
	/// <summary>
	///  To watch:
	///   - pinballx.ini
	///   - database xmls
	///   - database folders
	///   - table files
	/// </summary>
	public class FileSystemWatcher : IFileSystemWatcher
	{
		// dependencies
		private readonly Logger _logger;

		public FileSystemWatcher(Logger logger) {
			_logger = logger;
		}

		/// <summary>
		/// Returns an observable that will receive event when one specific
		/// file changes.
		/// </summary>
		/// <param name="filePath">Full path to file to watch</param>
		/// <returns></returns>
		public IObservable<string> FileWatcher(string filePath)
		{
			_logger.Info("Watching {0}", filePath);
			return (new FilesystemWatchCache()).Register(Path.GetDirectoryName(filePath), Path.GetFileName(filePath));
		}

		/// <summary>
		/// Returns an observable that will receive events when XML files within
		/// the database folder(s) change.
		/// </summary>
		/// 
		/// <remarks>
		/// Non-existent database folders will be ignored.
		/// </remarks>
		/// 
		/// <param name="dbPath">Path of PinballX's database folder</param>
		/// <param name="systems">List of systems to watch</param>
		/// <returns></returns>
		public IObservable<string> DatabaseWatcher(string dbPath, IList<PinballXSystem> systems)
		{
			IObservable<string> result = null;
			foreach (var sysPath in systems.Select(system => dbPath + system.Name + @"\").Where(Directory.Exists)) {
				_logger.Info("Watching {0}", sysPath);
				var watcher = (new FilesystemWatchCache()).Register(sysPath, "*.xml");
				result = result == null ? watcher : result.Merge(watcher);
			}
			return result;
		}
	}

	public interface IFileSystemWatcher
	{
		IObservable<string> FileWatcher(string filePath);
		IObservable<string> DatabaseWatcher(string dbPath, IList<PinballXSystem> systems);
	}
}
