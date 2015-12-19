using System.Reactive.Concurrency;
using System.Windows.Threading;
using VpdbAgent.Application;

namespace VpdbAgent.Tests.Mocks
{
	public class TestThreadManager : IThreadManager
	{
		public IScheduler WorkerScheduler { get; } = Scheduler.CurrentThread;
		public IScheduler CurrentThread { get; } = Scheduler.CurrentThread;
		public Dispatcher MainDispatcher { get; } = Dispatcher.CurrentDispatcher;
	}
}
