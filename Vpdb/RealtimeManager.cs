using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Newtonsoft.Json.Linq;
using NLog;
using PusherClient;
using ReactiveUI;

namespace VpdbAgent.Vpdb
{

	/// <summary>
	/// Provides a high-level API for receiving messages from Pusher.
	/// </summary>
	public interface IRealtimeManager
	{
		/// <summary>
		/// Produces a value when a release has been starred.
		/// </summary>
		IObservable<StarEvent> WhenReleaseStarred { get; }
		IObservable<NewVersionEvent> WhenReleaseUpdated { get; }
	}

	public class RealtimeManager : IRealtimeManager
	{
		// dependencies
		private readonly Logger _logger;

		// events
		public IObservable<StarEvent> WhenReleaseStarred { get; } = new Subject<StarEvent>();
		public IObservable<NewVersionEvent> WhenReleaseUpdated { get; } = new Subject<NewVersionEvent>();


		public RealtimeManager(IVpdbClient vpdbClient, Logger logger)
		{
			_logger = logger;

			// subscribe to pusher
			vpdbClient.UserChannel.Subscribe(OnChannelJoined);
		}

		/// <summary>
		/// Executed when the pusher connection with the private user channel
		/// is established and we can subscribe to messages.
		/// </summary>
		/// <param name="userChannel">User channel object</param>
		private void OnChannelJoined(Channel userChannel)
		{
			if (userChannel == null) {
				return;
			}

			// subscribe through a subject so we can do more fun stuff with it
			var star = new Subject<JObject>();
			var unstar = new Subject<JObject>();
			var newReleaseVersion = new Subject<JObject>();
			userChannel.Bind("star", data => { star.OnNext(data as JObject); });
			userChannel.Bind("unstar", data => { unstar.OnNext(data as JObject); });
			userChannel.Bind("new_release_version", data => { newReleaseVersion.OnNext(data as JObject); });

			// star
			star.ObserveOn(RxApp.MainThreadScheduler)
				.Select(msg => StarEvent.GetInstance(msg, true))
				.Where(msg => msg.Type == "release")
				.Subscribe((Subject<StarEvent>) WhenReleaseStarred);

			// unstar
			unstar.ObserveOn(RxApp.MainThreadScheduler)
				.Select(msg => StarEvent.GetInstance(msg, false))
				.Where(msg => msg.Type == "release")
				.Subscribe((Subject<StarEvent>) WhenReleaseStarred);

			// new release version
			newReleaseVersion.ObserveOn(RxApp.MainThreadScheduler)
				.Select(NewVersionEvent.GetInstance)
				.Subscribe((Subject<NewVersionEvent>)WhenReleaseUpdated);
		}
	}

	public class StarEvent
	{
		public string Id { get; private set; }
		public string Type { get; private set; }
		public bool Starred { get; private set; }
		public static StarEvent GetInstance(JObject data, bool starred)
		{
			return new StarEvent() {
				Id = data.GetValue("id").Value<string>(),
				Type = data.GetValue("type").Value<string>(),
				Starred = starred
			};
		}
	}

	public class NewVersionEvent
	{
		public string GameId { get; private set; }
		public string ReleaseId { get; private set; }
		public string VersionName { get; private set; }

		public static NewVersionEvent GetInstance(JObject data)
		{
			return new NewVersionEvent()
			{
				GameId = data.GetValue("game_id").Value<string>(),
				ReleaseId = data.GetValue("release_id").Value<string>(),
				VersionName = data.GetValue("version").Value<string>()
			};
		}
	}
}
