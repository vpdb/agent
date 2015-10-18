using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Common;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.ViewModels
{
	public class MainGameViewModel : ReactiveObject
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
		private IEnumerable<MainReleaseResultsItemViewModel> _identifiedReleases;
		public IEnumerable<MainReleaseResultsItemViewModel> IdentifiedReleases
		{
			get { return _identifiedReleases; }
			set { this.RaiseAndSetIfChanged(ref _identifiedReleases, value); }
		}

		// statuses
		private readonly ObservableAsPropertyHelper<bool> _isExecuting;
		public bool IsExecuting => _isExecuting.Value;
		private bool _hasExecuted;
		public bool HasExecuted
		{
			get { return _hasExecuted; }
			set { this.RaiseAndSetIfChanged(ref _hasExecuted, value); }
		}
		private bool _hasResults;
		public bool HasResults
		{
			get { return _hasResults; }
			set { this.RaiseAndSetIfChanged(ref _hasResults, value); }
		}

		public MainGameViewModel(Game game)
		{
			Game = game;

			// release identify
			IdentifyRelease = ReactiveCommand.CreateAsyncObservable(_ => VpdbClient.Api.GetReleasesBySize(game.FileSize, 512).SubscribeOn(Scheduler.Default));
			IdentifyRelease
				.Select(releases => releases.Select(release => new MainReleaseResultsItemViewModel(game, release, CloseResults)))
				.Subscribe(releases =>
				{
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
