using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VpdbAgent.Common;
using VpdbAgent.PinballX.Models;
using System.Reactive.Linq;

namespace VpdbAgent.PinballX
{
	public class FileSystemWatcher
	{
		private static FileSystemWatcher _instance;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// To watch:
		//   - pinballx.ini
		//   - database xmls
		//   - database folders
		//   - table files

		private FileSystemWatcher()
		{
		}

		/// <summary>
		/// Returns an observable that will receive event when one specific
		/// file changes.
		/// </summary>
		/// <param name="filePath">Full path to file to watch</param>
		/// <returns></returns>
		public IObservable<string> FileWatcher(string filePath)
		{
			Logger.Info("Watching {0}", filePath);
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
			foreach (var sysPath in systems.Select(system => dbPath + system.Name + @"\").Where(Directory.Exists))
			{
				Logger.Info("Watching {0}", sysPath);
				var watcher = (new FilesystemWatchCache()).Register(sysPath, "*.xml");
				result = result == null ? watcher : result.Merge(watcher);
			}
			return result;
		}

		public static FileSystemWatcher GetInstance()
		{
			return _instance ?? (_instance = new FileSystemWatcher());
		}
	}
}
