using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using VpdbAgent.Common;
using VpdbAgent.PinballX.Models;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using ReactiveUI;
using VpdbAgent.Common.Filesystem;
using Directory = System.IO.Directory;

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
		void WatchTables(IList<PinballXSystem> systems);

		IObservable<string> TableFolderAdded { get; }
		IObservable<string> TableFolderRemoved { get; }

		IObservable<string> TableFileCreated { get; }
		IObservable<string> TableFileChanged { get; }
		IObservable<Tuple<string, string>> TableFileRenamed { get; }
		IObservable<string> TableFileDeleted { get; }
	}

	[ExcludeFromCodeCoverage]
	public class FileSystemWatcher : IFileSystemWatcher
	{
		// observers
		public IObservable<string> TableFolderAdded => _tableFolderAdded;
		public IObservable<string> TableFolderRemoved => _tableFolderRemoved;

		public IObservable<string> TableFileCreated => _tableFileCreated;
		public IObservable<string> TableFileChanged => _tableFileChanged;
		public IObservable<Tuple<string, string>> TableFileRenamed => _tableFileRenamed;
		public IObservable<string> TableFileDeleted => _tableFileDeleted;

		// dependencies
		private readonly ILogger _logger;

		// internal props
		private readonly Subject<string> _tableFolderAdded = new Subject<string>();
		private readonly Subject<string> _tableFolderRemoved = new Subject<string>();
		private readonly Subject<string> _tableFileCreated = new Subject<string>();
		private readonly Subject<string> _tableFileChanged = new Subject<string>();
		private readonly Subject<Tuple<string, string>> _tableFileRenamed = new Subject<Tuple<string, string>>();
		private readonly Subject<string> _tableFileDeleted = new Subject<string>();

		private readonly Dictionary<string, IDisposable> _tableWatches = new Dictionary<string, IDisposable>();

		public FileSystemWatcher(ILogger logger) {
			_logger = logger;
		}

		public IObservable<string> FileWatcher(string filePath)
		{
			_logger.Info("Watching {0}", filePath);
			return (new FilesystemWatchCache()).Register(Path.GetDirectoryName(filePath), Path.GetFileName(filePath));
		}

		public void WatchTables(IList<PinballXSystem> systems)
		{
			const string pattern = @"^\.vp[tx]$";
			var trottle = TimeSpan.FromMilliseconds(100); // file changes are triggered multiple times
			var oldPaths = new HashSet<string>(_tableWatches.Keys);
			var newPaths = systems
				.Where(s => s.Enabled)
				.Select(s => PathHelper.NormalizePath(s.TablePath) + @"\")
				.Distinct()
				.Where(Directory.Exists);

			foreach (var newPath in newPaths) {
				var disposables = new CompositeDisposable();
				var fsw = new System.IO.FileSystemWatcher(newPath);
				disposables.Add(fsw);

				// start watching new paths
				if (!oldPaths.Contains(newPath)) {

					// file changed
					disposables.Add(Observable
							.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => fsw.Changed += x, x => fsw.Changed -= x)
							.Throttle(trottle, RxApp.TaskpoolScheduler)
							.Where(x => x.EventArgs.FullPath != null && Regex.IsMatch(Path.GetExtension(x.EventArgs.FullPath), pattern, RegexOptions.IgnoreCase))
							.Subscribe(x => _tableFileChanged.OnNext(x.EventArgs.FullPath)));

					// file created
					disposables.Add(Observable
							.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => fsw.Created += x, x => fsw.Created -= x)
							.Where(x => x.EventArgs.FullPath != null && Regex.IsMatch(Path.GetExtension(x.EventArgs.FullPath), pattern, RegexOptions.IgnoreCase))
							.Subscribe(x => _tableFileCreated.OnNext(x.EventArgs.FullPath)));

					// file deleted
					disposables.Add(Observable
							.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => fsw.Deleted += x, x => fsw.Deleted -= x)
							.Where(x => x.EventArgs.FullPath != null && Regex.IsMatch(Path.GetExtension(x.EventArgs.FullPath), pattern, RegexOptions.IgnoreCase))
							.Subscribe(x => _tableFileDeleted.OnNext(x.EventArgs.FullPath)));

					// file renamed
					disposables.Add(Observable
							.FromEventPattern<RenamedEventHandler, FileSystemEventArgs>(x => fsw.Renamed += x, x => fsw.Renamed -= x)
							.Throttle(trottle, RxApp.TaskpoolScheduler)
							.Where(x => x.EventArgs.FullPath != null && Regex.IsMatch(Path.GetExtension(x.EventArgs.FullPath), pattern, RegexOptions.IgnoreCase))
							.Subscribe(x => _tableFileRenamed.OnNext(new Tuple<string, string>(((RenamedEventArgs)x.EventArgs).OldFullPath, x.EventArgs.FullPath))));

					fsw.EnableRaisingEvents = true;

					_tableWatches.Add(newPath, disposables);
					_tableFolderAdded.OnNext(newPath);
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
				_tableFolderRemoved.OnNext(oldPath);
				_logger.Info("Stopped watching table folder {0}", oldPath);
			}
		}
	}
}
