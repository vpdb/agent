using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Common.Extensions
{
	public static class Rx
	{

		public static IObservable<T> StepInterval<T>(this IObservable<T> source, TimeSpan minDelay)
		{
			return source.Select(x =>
				Observable.Empty<T>()
					.Delay(minDelay)
					.StartWith(x)
			).Concat();
		}

	}
}
