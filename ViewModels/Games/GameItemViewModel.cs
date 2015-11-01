using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.ViewModels.Games
{
	public class GameItemViewModel : ReactiveObject
	{
		// deps
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();
		private static readonly IVpdbClient VpdbClient = Locator.CurrentMutable.GetService<IVpdbClient>();

		// commands
		public ReactiveCommand<List<Release>> IdentifyRelease { get; protected set; }
		public ReactiveCommand<object> CloseResults { get; protected set; } = ReactiveCommand.Create();

		// data
		public Game Game { get; }

		// needed for filters
		private bool _isVisible = true;
		public bool IsVisible { get { return _isVisible; } set { this.RaiseAndSetIfChanged(ref _isVisible, value); } }

		// release search results
		private IEnumerable<GameResultItemViewModel> _identifiedReleases;
		public IEnumerable<GameResultItemViewModel> IdentifiedReleases { get { return _identifiedReleases; } set { this.RaiseAndSetIfChanged(ref _identifiedReleases, value); } }

		// statuses
		public bool IsExecuting => _isExecuting.Value;
		public bool HasExecuted { get { return _hasExecuted; } set { this.RaiseAndSetIfChanged(ref _hasExecuted, value); } }
		public bool HasResults { get { return _hasResults; } set { this.RaiseAndSetIfChanged(ref _hasResults, value); } }
		private readonly ObservableAsPropertyHelper<bool> _isExecuting;
		private bool _hasExecuted;
		private bool _hasResults;

		public GameItemViewModel(Game game)
		{
			Game = game;

			// release identify
			IdentifyRelease = ReactiveCommand.CreateAsyncObservable(_ => VpdbClient.Api.GetReleasesBySize(game.FileSize, 1000000).SubscribeOn(Scheduler.Default));
			IdentifyRelease.Select(releases => releases
				.Select(release => new {release, release.Versions})
				.SelectMany(x => x.Versions.Select(version => new {x.release, version, version.Files}))
				.SelectMany(x => x.Files.Select(file => new GameResultItemViewModel(game, x.release, x.version, file, CloseResults)))
			).Subscribe(releases => {

				// todo try direct match if filename AND size matches.
				IdentifiedReleases = releases;
			});

			// handle errors
			IdentifyRelease.ThrownExceptions.Subscribe(e => { Logger.Error(e, "Error matching game."); });

			// spinner
			IdentifyRelease.IsExecuting.ToProperty(this, vm => vm.IsExecuting, out _isExecuting);

			IdentifyRelease.IsExecuting
				.Skip(1)             // skip initial false value
				.Where(x => !x)      // then trigger when false again
				.Subscribe(_ => { HasExecuted = true; });

			IdentifyRelease.Select(r => r.Count > 0).Subscribe(hasResults => { HasResults = hasResults; });

			// close button
			CloseResults.Subscribe(_ => { HasExecuted = false; });
		}

		public override string ToString()
		{
			return $"[GameViewModel] {Game}";
		}
	}
}
