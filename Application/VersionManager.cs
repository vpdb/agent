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
		public const string UpdateFolder = @"C:\dev\vpdb-agent\Releases";
		public static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(5);

		public IObservable<ReleaseEntry> NewVersionAvailable => _newVersionAvailable;

		private readonly BehaviorSubject<ReleaseEntry> _newVersionAvailable = new BehaviorSubject<ReleaseEntry>(null);

		private readonly Logger _logger;

		public VersionManager(Logger logger)
		{
			_logger = logger;
		}

		public IVersionManager Initialize()
		{
			// setup update check beginning with now
			Observable.Interval(UpdateInterval).StartWith(0).Subscribe(CheckForUpdate);

			return this;
		}

		private void CheckForUpdate(long x)
		{
#if DEBUG
			_logger.Info("Not checking for updates in debug mode but we'll create one anyway ({0})", x);
			if (x > 0) {
				OnNewVersionAvailable(ReleaseEntry.ParseReleaseEntry("83E9F5BEB4079D5B01C573CA1DFC5CBFCB6899CF VpdbAgent-1.0.0-full.nupkg 2851039"));
			}
#else
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
