using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Windows.Threading;

namespace VpdbAgent.Application
{
	/// <summary>
	/// An interface providing the correct schedulers and that can be switched
	/// out when running unit tests.
	/// </summary>
	public interface IThreadManager
	{
		/// <summary>
		/// The "worker" scheduler, <see cref="Scheduler.Default"/>.
		/// </summary>
		IScheduler WorkerScheduler { get; }

		/// <summary>
		/// The "main" scheduler, <see cref="Scheduler.CurrentThread"/>
		/// </summary>
		IScheduler CurrentThread { get; }

		/// <summary>
		/// The "main" dispatcher, <see cref="Dispatcher"/>
		/// </summary>
		Dispatcher MainDispatcher { get; }
	}

	[ExcludeFromCodeCoverage]
	public class ThreadManager : IThreadManager
	{
		public IScheduler WorkerScheduler => Scheduler.Default;
		public IScheduler CurrentThread => Scheduler.CurrentThread;
		public Dispatcher MainDispatcher => System.Windows.Application.Current.Dispatcher;
	}
}
