using System;
using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace VpdbAgent.Libs
{
	/// <summary>
	/// A class which does not automatically start queued jobs but
	/// requires a user of the class to explicitely start new jobs by calling
	/// StartNext() or StartUpTo(maxConcurrentCount).
	/// </summary>
	/// <a href="https://social.msdn.microsoft.com/Forums/en-US/2817c6e5-e5a4-4aac-91c1-97ba7de88ff7/nonblocking-jobqueue-and-paralleljobqueue-sample?forum=rx">Source</a>
	public class JobQueue
	{
		public struct Job
		{
			public Func<IObservable<Unit>> AsyncStart;
			public AsyncSubject<Unit> CompletionHandler;
			public BooleanDisposable Cancel;
			public MultipleAssignmentDisposable JobSubscription;
		}

		int _runningCount;
		readonly ConcurrentQueue<Job> _queue = new ConcurrentQueue<Job>();
		readonly Subject<Unit> _whenQueueEmpty = new Subject<Unit>();
		readonly Subject<Notification<Unit>> _whenJobCompletes = new Subject<Notification<Unit>>();
		readonly Subject<Exception> _whenJobFails = new Subject<Exception>();

		public IObservable<Notification<Unit>> WhenJobCompletes => _whenJobCompletes.AsObservable();
		public IObservable<Unit> WhenQueueEmpty => _whenQueueEmpty.AsObservable();
		public IObservable<Exception> WhenJobFails => _whenJobFails;
		public int RunningCount => _runningCount;
		public int QueuedCount => _queue.Count;

		public JobQueue()
		{
			// whenJobFails subscription
			_whenJobCompletes.Where(n => n.Kind == NotificationKind.OnError)
				.Select(n => n.Exception)
				.Subscribe(_whenJobFails);

			// whenQueueEmpty subscription
			_whenJobCompletes.Synchronize(this)
				.Where(n => _queue.Count == 0 && _runningCount == 0)
				.Select(n => new Unit()).Subscribe(_whenQueueEmpty);
		}

		public IObservable<Unit> Add(Action action)
		{
			return Add(action.ToAsync());
		}

		public IObservable<Unit> Add(Func<IObservable<Unit>> asyncStart)
		{
			var job = new Job() {
				AsyncStart = asyncStart,
				CompletionHandler = new AsyncSubject<Unit>(),
				Cancel = new BooleanDisposable(),
				JobSubscription = new MultipleAssignmentDisposable()
			};

			var cancelable = Observable.Create<Unit>(o =>
				new CompositeDisposable(
					job.CompletionHandler.Subscribe(o),     // main job subscription
					job.JobSubscription,
					job.Cancel)
			);

			job.CompletionHandler
				.Materialize()
				.Where(n => n.Kind == NotificationKind.OnCompleted || n.Kind == NotificationKind.OnError)
				.Subscribe(_whenJobCompletes.OnNext);        // pass on errors and completions

			_queue.Enqueue(job);
			return cancelable;
		}

		public int StartUpTo(int maxConcurrentlyRunning)
		{
			var started = 0;
			for (;;) {
				for (;;) {
					int running;

					do   // test and increment with compare and swap
					{
						running = _runningCount;
						if (running >= maxConcurrentlyRunning)
							return started;
					} while (Interlocked.CompareExchange(ref _runningCount, running + 1, running) != running);

					Job job;
					if (TryDequeNextJob(out job)) {
						StartJob(job);
						++started;
					} else {
						// dequeing job failed but we already incremented running count
						Interlocked.Decrement(ref _runningCount);

						// ensure that no other thread queued an item and did not start it
						// because the running count was too high
						if (_queue.Count == 0) {
							// if there is nothing in the queue after the decrement 
							// we can safely return
							return started;
						}
					}
				}
			}
		}

		public bool StartNext()
		{
			Job job;
			if (TryDequeNextJob(out job)) {
				Interlocked.Increment(ref _runningCount);
				StartJob(job);
				return true;
			}

			return false;
		}

		bool TryDequeNextJob(out Job job)
		{
			do {
				if (!_queue.TryDequeue(out job))
					return false;
			} while (job.Cancel.IsDisposed);
			return true;
		}

		void StartJob(Job job)
		{
			try {
				var jobSubscription =
					job.AsyncStart().Subscribe(
						u => OnJobCompleted(job, null),
						e => OnJobCompleted(job, e)
					);
				job.JobSubscription.Disposable = jobSubscription;

				if (job.Cancel.IsDisposed)
					job.JobSubscription.Dispose();
			} catch (Exception ex) {
				OnJobCompleted(job, ex);
				throw;
			}
		}

		public void CancelOutstandingJobs()
		{
			Job job;
			while (TryDequeNextJob(out job))
				job.CompletionHandler.OnError(new OperationCanceledException());
		}

		void OnJobCompleted(Job job, Exception error)
		{
			Interlocked.Decrement(ref _runningCount);

			if (error == null)
				job.CompletionHandler.OnNext(new Unit());
			else
				job.CompletionHandler.OnError(error);
		}
	}

	/// <summary>
	/// A class which uses the JobQueue internally and starts new jobs as soon
	/// as the number of currently running jobs drops below a given threshold.
	/// </summary>
	public class ParallelJobQueue
	{
		readonly int _maxConcurrent;

		public ParallelJobQueue(int maxConcurrent)
		{
			if (maxConcurrent < 1)
				throw new ArgumentOutOfRangeException(nameof(maxConcurrent));

			_maxConcurrent = maxConcurrent;
			InnerQueue = new JobQueue();
			InnerQueue.WhenJobCompletes.Subscribe(OnJobCompleted);
		}

		public JobQueue InnerQueue { get; private set; }

		public IObservable<Unit> Add(Action action)
		{
			return Add(action.ToAsync());
		}

		public IObservable<Unit> Add(Func<IObservable<Unit>> asyncStart)
		{
			var whenCompletes = InnerQueue.Add(asyncStart);
			InnerQueue.StartUpTo(_maxConcurrent);
			return whenCompletes;
		}

		/// <summary>
		/// Stops starting new jobs of the old queue by replacing 
		/// the inner queue with an empty new one.
		/// </summary>
		public void Stop()
		{
			var oldQueue = InnerQueue;
			InnerQueue = new JobQueue();
			oldQueue.CancelOutstandingJobs();
		}

		void OnJobCompleted(Notification<Unit> notification)
		{
			InnerQueue.StartUpTo(_maxConcurrent);
		}
	}
}

