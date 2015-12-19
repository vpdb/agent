using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace VpdbAgent.Application
{
	public interface IThreadManager
	{
		IScheduler WorkerScheduler { get; }
		IScheduler CurrentThread { get; }

		Dispatcher MainDispatcher { get; }
	}

	public class ThreadManager : IThreadManager
	{
		public IScheduler WorkerScheduler => Scheduler.Default;
		public IScheduler CurrentThread => Scheduler.CurrentThread;
		public Dispatcher MainDispatcher => System.Windows.Application.Current.Dispatcher;
	}
}
