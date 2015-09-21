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

		// To watch:
		//   - pinballx.ini
		//   - database xmls
		//   - database folders
		//   - table files
		public delegate void IniChangedHandler();
		public event IniChangedHandler IniChanged;

		private Dictionary<string, long> lastUpdate = new Dictionary<string, long>();

		private FileWatcher()
		{
		}

		public void SetupIni(string path)
		{
			if (path == null) {
				return;
			}
			Console.WriteLine("Watching {0}", path);
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
				IniChanged();
			}
		}


		public static FileWatcher GetInstance()
		{
			if (INSTANCE == null) {
				INSTANCE = new FileWatcher();
			}
			return INSTANCE;
		}

	}
}
// To watch:
//   - pinballx.ini
//   - database xmls
//   - database folders
//   - table files