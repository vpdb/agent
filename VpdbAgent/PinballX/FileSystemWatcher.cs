using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using VpdbAgent.Common;
using VpdbAgent.PinballX.Models;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
		/// a given folder.
		/// </summary>
		/// 
		/// <remarks>
		/// Non-existent database folders will be ignored.
		/// </remarks>
		/// 
		/// <param name="dbPath">Path of PinballX's database folder</param>
		/// <param name="system">System to watch</param>
		/// <returns>Observable that receives the absolute path of the database file that changed</returns>
		IObservable<string> FolderWatcher(string dbPath, PinballXSystem system);
		
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
		[Obsolete("Use FolderWatcher.")]
		IObservable<string> DatabaseWatcher(string dbPath, IList<PinballXSystem> systems);

		/// <summary>
		/// Watches all table files for all provided systems.
		/// </summary>
		/// <remarks>
		/// Note that multiple systems can point to the same table folder, so all 
		/// that is returned is the path of the changed file and the system where it
		/// belongs to has to be found out from there.
		/// 
		/// Also note that always the same observable is returned and removed watches
		/// are automatically disposed, so there is no need to dispose this Oberservable.
		/// </remarks>
		/// <param name="systems">Systems</param>
		/// <returns>Observable that receives the absolute path of any changed table file</returns>
		IObservable<string> WatchTables(IList<PinballXSystem> systems);
	}

	[ExcludeFromCodeCoverage]
	public class FileSystemWatcher : IFileSystemWatcher
	{
		// dependencies
		private readonly ILogger _logger;

		// internal props
		private readonly Dictionary<string, IDisposable> _tableWatches = new Dictionary<string, IDisposable>();
		private readonly Subject<string> _tableWatcher = new Subject<string>();

		public FileSystemWatcher(ILogger logger) {
			_logger = logger;
		}

		public IObservable<string> FileWatcher(string filePath)
		{
			_logger.Info("Watching {0}", filePath);
			return (new FilesystemWatchCache()).Register(Path.GetDirectoryName(filePath), Path.GetFileName(filePath));
		}

		public IObservable<string> FolderWatcher(string dbPath, PinballXSystem system)
		{
			var sysPath = dbPath + system.Name  + @"\";
			const string filter = "*.xml";
			if (Directory.Exists(sysPath)) {
				_logger.Info("Watching {0}{1}", sysPath, filter);
				return (new FilesystemWatchCache()).Register(sysPath, filter);
			}
			return new Subject<string>();
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

		public IObservable<string> WatchTables(IList<PinballXSystem> systems)
		{
			const string pattern = @"^\.vp[tx]$";
			var oldPaths = new HashSet<string>(_tableWatches.Keys);
			var newPaths = systems
				.Where(s => s.Enabled)
				.Select(s => s.TablePath + @"\")
				.Distinct()
				.Where(Directory.Exists);

			foreach (var newPath in newPaths) {
				
				// start watching new paths
				if (!oldPaths.Contains(newPath)) {
					var watcher = (new FilesystemWatchCache()).Register(newPath)
						.Where(f => Regex.IsMatch(Path.GetExtension(f), pattern, RegexOptions.IgnoreCase))
						.Subscribe(_tableWatcher.OnNext);
					_tableWatches.Add(newPath, watcher);
					_logger.Info("Started watching table folder {0}", newPath);

				// ignore already watching paths
				} else {
					oldPaths.Remove(newPath);
				}
			}
			// stop watching non-provided paths
			foreach (var oldPath in oldPaths) {
				_tableWatches[oldPath].Dispose();
				_tableWatches.Remove(oldPath);
				_logger.Info("Stopped watching table folder {0}", oldPath);
			}

			return _tableWatcher;
		}
	}
}
