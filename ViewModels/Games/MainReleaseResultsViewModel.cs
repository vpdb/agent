using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.ViewModels.Games
{
	public class MainReleaseResultsViewModel : ReactiveObject
	{
		// deps
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();

		// public props
		public Game Game { get; set; }

		// release search results
		private readonly ObservableAsPropertyHelper<IEnumerable<MainReleaseResultsItemViewModel>> _identifiedReleases;
		public IEnumerable<MainReleaseResultsItemViewModel> IdentifiedReleases => _identifiedReleases.Value;

		// visibility
		private readonly ObservableAsPropertyHelper<bool> _hasResults;
		public bool HasResults => _hasResults.Value;
		private bool _hasExecuted;
		public bool HasExecuted
		{
			get { return _hasExecuted; }
			set { this.RaiseAndSetIfChanged(ref _hasExecuted, value); }
		}

		// commands
		public ReactiveCommand<object> CloseResults { get; protected set; } = ReactiveCommand.Create();

		public MainReleaseResultsViewModel(Game game, IReactiveCommand<List<Release>> identifyRelease) {
			Game = game;

			// link results to property
			identifyRelease
				.Select(releases => releases.Select(release => new MainReleaseResultsItemViewModel(game, release, CloseResults)))
				.ToProperty(this, vm => vm.IdentifiedReleases, out _identifiedReleases);

			// handle errors
			identifyRelease.ThrownExceptions.Subscribe(e => { Logger.Error(e, "Error matching game."); });

			// handle visibility & expansion status
			identifyRelease.Select(releases => releases.Count > 0).ToProperty(this, vm => vm.HasResults, out _hasResults);
			identifyRelease.IsExecuting
				.Skip(1) // skip initial false value
				.Where(x => !x) // then trigger when false again
				.Subscribe(_ => { HasExecuted = true; });

			// close button
			CloseResults.Subscribe(_ => { HasExecuted = false; });
		}
	}
}
