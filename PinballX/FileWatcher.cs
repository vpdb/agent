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
	public class FileWatcher
	{
		private static FileWatcher _instance;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// To watch:
		//   - pinballx.ini
		//   - database xmls
		//   - database folders
		//   - table files

		private FileWatcher()
		{
		}

		#region Ini
		public IObservable<string> SetupIni(string path)
		{
			Logger.Info("Watching {0}", path);
			return (new FilesystemWatchCache()).Register(Path.GetDirectoryName(path), Path.GetFileName(path));
		}
		#endregion

		#region Xml
		public IObservable<string> SetupXml(string path, List<PinballXSystem> systems)
		{

			IObservable<string> result = null;
			foreach (var sysPath in systems.Select(system => path + system.Name + @"\").Where(Directory.Exists))
			{
				Logger.Info("Watching {0}", sysPath);
				var watcher = (new FilesystemWatchCache()).Register(sysPath, "*.xml");
				result = result == null ? watcher : result.Merge(watcher);
			}
			return result;
		}
		#endregion

		public static FileWatcher GetInstance()
		{
			return _instance ?? (_instance = new FileWatcher());
		}
	}
}
