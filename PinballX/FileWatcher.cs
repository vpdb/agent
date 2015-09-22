using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

		// event handler for PinballX.ini
		public delegate void IniChangedHandler();
		public event IniChangedHandler IniChanged;

		// event handler for Databases/{system}/*.xml
		public delegate void XmlChangedHandler(string path, WatcherChangeTypes type);
		public event XmlChangedHandler XmlChanged;
		private List<FileSystemWatcher> xmlWatchers = new List<FileSystemWatcher>();

		private Dictionary<string, long> lastUpdate = new Dictionary<string, long>();

		private FileWatcher()
		{
		}

		#region Ini
		public void SetupIni(string path)
		{
			if (path == null) {
				return;
			}
			logger.Info("Watching {0}", path);
			FileSystemWatcher watcher = new FileSystemWatcher();
			watcher.Path = Path.GetDirectoryName(path);
			watcher.Filter = Path.GetFileName(path);
			watcher.Changed += new FileSystemEventHandler(OnIniChanged);

			watcher.EnableRaisingEvents = true;
		}

		private void OnIniChanged(object source, FileSystemEventArgs e)
		{
			long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			if (!lastUpdate.ContainsKey("ini") || now - lastUpdate["ini"] > THRESHOLD) {
				lastUpdate["ini"] = now;
				if (IniChanged != null) {
					IniChanged();
				}
			}
		}
		#endregion

		#region Xml
		public void SetupXml(string path, List<PinballXSystem> systems)
		{
			if (path == null) {
				return;
			}

			// dispose current watchers
			foreach (FileSystemWatcher watcher in xmlWatchers) {
				watcher.EnableRaisingEvents = false;
				watcher.Dispose();
			}
			xmlWatchers.Clear();

			// add new watchers
			foreach (PinballXSystem system in systems) {
				string systemPath = path + system.Name;
				logger.Info("Watching {0}", systemPath);

				FileSystemWatcher watcher = new FileSystemWatcher();
				watcher.Path = Path.GetDirectoryName(systemPath);
				watcher.Filter = "*.xml";
				watcher.Created += new FileSystemEventHandler(OnXmlChanged);
				watcher.Changed += new FileSystemEventHandler(OnXmlChanged);
				watcher.Deleted += new FileSystemEventHandler(OnXmlChanged);
				watcher.EnableRaisingEvents = true;

				xmlWatchers.Add(watcher);
			}
		}

		private void OnXmlChanged(object source, FileSystemEventArgs e)
		{
			string xmlFilePath = e.FullPath;
			long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
			if (!lastUpdate.ContainsKey(xmlFilePath) || now - lastUpdate[xmlFilePath] > THRESHOLD) {
				lastUpdate[xmlFilePath] = now;

				if (XmlChanged != null) {
					XmlChanged(xmlFilePath, e.ChangeType);
				}
			}
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
