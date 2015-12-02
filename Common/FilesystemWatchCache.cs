using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Splat;

namespace VpdbAgent.Common
{
	interface IFilesystemWatchCache
	{
		IObservable<string> Register(string directory, string filter = null);
	}

	/// <summary>
	/// Wraps the file observer into an observable.
	/// </summary>
	public class FilesystemWatchCache : IFilesystemWatchCache
	{
		readonly MemoizingMRUCache<Tuple<string, string>, IObservable<string>> _watchCache = new MemoizingMRUCache<Tuple<string, string>, IObservable<string>>((pair, _) =>
		{
			return Observable.Create<string>(subj =>
			{
				var disp = new CompositeDisposable();

				var fsw = pair.Item2 != null ?
					new FileSystemWatcher(pair.Item1, pair.Item2) :
					new FileSystemWatcher(pair.Item1);

				disp.Add(fsw);

				var allEvents = Observable.Merge(
					Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => fsw.Changed += x, x => fsw.Changed -= x),
					Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => fsw.Created += x, x => fsw.Created -= x),
					Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(x => fsw.Deleted += x, x => fsw.Deleted -= x),
					Observable.FromEventPattern<RenamedEventHandler, FileSystemEventArgs>(x => fsw.Renamed += x, x => fsw.Renamed-= x));

				disp.Add(allEvents
					.Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
					.SelectMany(x => x.EventArgs.ChangeType == WatcherChangeTypes.Renamed ? 
						new List<string>() { ((RenamedEventArgs)x.EventArgs).OldFullPath, x.EventArgs.FullPath } : 
						new List<string>() { x.EventArgs.FullPath })
					.Synchronize(subj)
					.Subscribe(subj)
				);

				fsw.EnableRaisingEvents = true;
				return disp;
			}).Publish().RefCount();
		}, 25);

		public IObservable<string> Register(string directory, string filter = null)
		{
			lock (_watchCache) {
				return _watchCache.Get(Tuple.Create(directory, filter));
			}
		}
	}
}
