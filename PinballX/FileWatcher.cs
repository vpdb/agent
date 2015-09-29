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
		private static readonly int THRESHOLD = 1000;
		private static FileWatcher INSTANCE;
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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
			logger.Info("Watching {0}", path);
			return (new FilesystemWatchCache()).Register(Path.GetDirectoryName(path), Path.GetFileName(path));
		}
		#endregion

		#region Xml
		public IObservable<string> SetupXml(string path, List<PinballXSystem> systems)
		{

			IObservable<string> result = null;
			foreach (PinballXSystem system in systems) {
				string systemPath = path + system.Name + @"\";
				if (Directory.Exists(systemPath)) {
					logger.Info("Watching {0}", systemPath);
					var watcher = (new FilesystemWatchCache()).Register(systemPath, "*.xml");
					if (result == null) {
						result = watcher;
					} else {
						result = result.Concat(watcher);
					}
				}
			}
			return result;
		}
		#endregion

		public static FileWatcher GetInstance()
		{
			if (INSTANCE == null) {
				INSTANCE = new FileWatcher();
			}
			return INSTANCE;
		}

	}
}
