using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using VpdbAgent.Common;
using VpdbAgent.PinballX.Models;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace VpdbAgent.PinballX
{

	/// <summary>
	/// A class that converterts needed file system changes into observables.
	/// 
	/// There are methods for watching:
	/// 
	///   - PinballX.ini
	///   - XML files under the Database folder
	///   - Table files under the Tables folder
	/// 
	/// </summary>
	public interface IFileSystemWatcher
	{
		/// <summary>
		/// Returns an observable that will receive event when one specific
		/// file changes.
		/// </summary>
		/// <param name="filePath">Full path to file to watch</param>
		/// <returns></returns>
		IObservable<string> FileWatcher(string filePath);

		/// <summary>
		/// Returns an observable that will receive events when XML files within
		/// any of the provided systems' database folders change.
		/// </summary>
		/// 
		/// <remarks>
		/// Non-existent database folders will be ignored.
		/// </remarks>
		/// 
		/// <param name="dbPath">Path of PinballX's database folder</param>
		/// <param name="systems">List of systems to watch</param>
		/// <returns>Observable that receives the absolute path of the database file that changed</returns>
		IObservable<string> DatabaseWatcher(string dbPath, IList<PinballXSystem> systems);

		/// <summary>
		/// Watches all table files for all enabled systems.
		/// </summary>
		/// <remarks>
		/// Note that multiple systems can point to the same table folder, so all 
		/// that is returned is the path of the changed file and the system where it
		/// belongs to has to be found out from there.
		/// </remarks>
		/// <param name="systems">Systems</param>
		/// <returns>Observable that receives the absolute path of any changed table file</returns>
		IObservable<string> TablesWatcher(IList<PinballXSystem> systems);
	}

	[ExcludeFromCodeCoverage]
	public class FileSystemWatcher : IFileSystemWatcher
	{
		// dependencies
		private readonly Logger _logger;

		public FileSystemWatcher(Logger logger) {
			_logger = logger;
		}

		public IObservable<string> FileWatcher(string filePath)
		{
			_logger.Info("Watching {0}", filePath);
			return (new FilesystemWatchCache()).Register(Path.GetDirectoryName(filePath), Path.GetFileName(filePath));
		}

		public IObservable<string> DatabaseWatcher(string dbPath, IList<PinballXSystem> systems)
		{
			const string filter = "*.xml";
			IObservable<string> result = null;
			foreach (var sysPath in systems.Select(system => dbPath + system.Name + @"\").Where(Directory.Exists)) {
				_logger.Info("Watching {0}{1}", sysPath, filter);
				var watcher = (new FilesystemWatchCache()).Register(sysPath, filter);
				result = result == null ? watcher : result.Merge(watcher);
			}
			return result;
		}

		public IObservable<string> TablesWatcher(IList<PinballXSystem> systems)
		{
			const string pattern = @"^\.vp[tx]$";
			IObservable<string> result = null;
			systems
				.Where(s => s.Enabled)
				.Select(s => s.TablePath + @"\")
				.Distinct()
				.Where(Directory.Exists)
				.ToList()
				.ForEach(sysPath => {
					_logger.Info("Watching {0}*.vp[tx]", sysPath);
					var watcher = (new FilesystemWatchCache()).Register(sysPath);
					result = result == null ? watcher : result.Merge(watcher);
				});

			return result.Where(f => Regex.IsMatch(Path.GetExtension(f), pattern, RegexOptions.IgnoreCase));
		}
	}
}
