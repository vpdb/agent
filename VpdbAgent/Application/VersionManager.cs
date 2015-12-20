using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using NLog;
using Squirrel;

namespace VpdbAgent.Application
{
	public interface IVersionManager
	{
		IObservable<ReleaseEntry> NewVersionAvailable { get; }
		IVersionManager Initialize();
	}

	public class VersionManager : IVersionManager
	{
		public const string UpdateFolder = "https://raw.githubusercontent.com/freezy/vpdb-agent/master/Releases"; // @"C:\dev\vpdb-agent\Releases";
		public static readonly TimeSpan UpdateInterval = TimeSpan.FromDays(1);

		private readonly Logger _logger;
		private readonly CrashManager _crashManager;

		public IObservable<ReleaseEntry> NewVersionAvailable => _newVersionAvailable;

		private readonly BehaviorSubject<ReleaseEntry> _newVersionAvailable = new BehaviorSubject<ReleaseEntry>(null);

		public VersionManager(CrashManager crashManager, Logger logger)
		{
			_logger = logger;
			_crashManager = crashManager;
		}

		public IVersionManager Initialize()
		{
			// setup update check beginning with now
			Observable.Interval(UpdateInterval).StartWith(0).Subscribe(CheckForUpdate);
			return this;
		}

		private void CheckForUpdate(long x)
		{
#if !DEBUG
			Task.Run(async () => {
				try {
					using (var mgr = new UpdateManager(UpdateFolder)) {
						var release = await mgr.UpdateApp();
						if (release.Version > mgr.CurrentlyInstalledVersion()) {
							OnNewVersionAvailable(release);
						}
					}
				} catch (Exception e) {
					_logger.Error(e, "Failed checking for updates: {0}", e.Message);
					_crashManager.Report(e, "squirrel");
				}
			});
#endif
		}

		private void OnNewVersionAvailable(ReleaseEntry release)
		{
			_logger.Info("New version {0} available!", release.Version);
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				_newVersionAvailable.OnNext(release);
			});
		}

	}
}
