using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.ViewModels.Games
{
	public class GameItemViewModel : ReactiveObject
	{
		private const long MatchThreshold = 512;

		// deps
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();
		private static readonly IVpdbClient VpdbClient = Locator.CurrentMutable.GetService<IVpdbClient>();
		private static readonly IGameManager GameManager = Locator.CurrentMutable.GetService<IGameManager>();
		private static readonly IMessageManager MessageManager = Locator.CurrentMutable.GetService<IMessageManager>();

		// commands
		public ReactiveCommand<List<VpdbRelease>> IdentifyRelease { get; protected set; }
		public ReactiveCommand<object> CloseResults { get; } = ReactiveCommand.Create();
		public ReactiveCommand<object> SyncToggled { get; } = ReactiveCommand.Create();

		// data
		public Game Game { get; }
		public VpdbVersion Version => _version.Value;
		public VpdbTableFile TableFile => _file.Value;

		// needed for filters
		private bool _isVisible = true;
		public bool IsVisible { get { return _isVisible; } set { this.RaiseAndSetIfChanged(ref _isVisible, value); } }

		// release search results
		private IEnumerable<GameResultItemViewModel> _identifiedReleases;
		public IEnumerable<GameResultItemViewModel> IdentifiedReleases { get { return _identifiedReleases; } set { this.RaiseAndSetIfChanged(ref _identifiedReleases, value); } }

		// statuses
		public bool IsExecuting => _isExecuting.Value;
		public bool ShowIdentifyButton => _showIdentifyButton.Value;
		public bool HasExecuted { get { return _hasExecuted; } set { this.RaiseAndSetIfChanged(ref _hasExecuted, value); } }
		public bool HasResults { get { return _hasResults; } set { this.RaiseAndSetIfChanged(ref _hasResults, value); } }

		private readonly ObservableAsPropertyHelper<bool> _showIdentifyButton;
		private readonly ObservableAsPropertyHelper<bool> _isExecuting;
		private readonly ObservableAsPropertyHelper<VpdbVersion> _version;
		private readonly ObservableAsPropertyHelper<VpdbTableFile> _file;
		private bool _hasExecuted;
		private bool _hasResults;

		public GameItemViewModel(Game game)
		{
			Game = game;

			// find file object in release
			this.WhenAnyValue(vm => vm.Game.Release)
				.Where(r => r != null)
				.SelectMany(r => r.Versions)
				.SelectMany(v => v.Files)
				.Where(f => f.Reference.Id.Equals(Game.FileId))
				.ToProperty(this, vm => vm.TableFile, out _file);

			// find version object in release
			this.WhenAnyValue(vm => vm.Game.Release)
				.Where(r => r != null)
				.SelectMany(r => r.Versions)
				.SelectMany(v => v.Files.Select(f => new { v, f }))
				.Where(x => x.f.Reference.Id.Equals(Game.FileId))
				.Select(x => x.v)
				.ToProperty(this, vm => vm.Version, out _version);

			// release identify
			IdentifyRelease = ReactiveCommand.CreateAsyncObservable(_ => VpdbClient.Api.GetReleasesBySize(game.FileSize, MatchThreshold).SubscribeOn(Scheduler.Default));
			IdentifyRelease.Select(releases => releases
				.Select(release => new {release, release.Versions})
				.SelectMany(x => x.Versions.Select(version => new {x.release, version, version.Files}))
				.SelectMany(x => x.Files.Select(file => new GameResultItemViewModel(game, x.release, x.version, file, CloseResults)))
			).Subscribe(x => {

				var releases = x as GameResultItemViewModel[] ?? x.ToArray();
				var numMatches = 0;
				GameResultItemViewModel match = null;
				foreach (var vm in releases) {
					if (game.Filename.Equals(vm.TableFile.Reference.Name) && game.FileSize == vm.TableFile.Reference.Bytes) {
						numMatches++;
						match = vm;
					}
				}

				// if file name and file size are identical, directly match.
				if (numMatches == 1 && match != null) {
					GameManager.LinkRelease(match.Game, match.Release, match.TableFile.Reference.Id);
					MessageManager.LogReleaseLinked(match.Game, match.Release, match.TableFile.Reference.Id);

				} else {
					IdentifiedReleases = releases;
					HasExecuted = true;
				}
			}, exception => VpdbClient.HandleApiError(exception, "identifying a game by file size"));

			//SyncToggled
			//	.Where(_ => Game.IsSynced && Game.HasRelease)
			//	.Subscribe(_ => { GameManager.Sync(Game); });

			// handle errors
			IdentifyRelease.ThrownExceptions.Subscribe(e => { Logger.Error(e, "Error matching game."); });

			// spinner
			IdentifyRelease.IsExecuting.ToProperty(this, vm => vm.IsExecuting, out _isExecuting);

			// result switch
			IdentifyRelease.Select(r => r.Count > 0).Subscribe(hasResults => { HasResults = hasResults; });

			// close button
			CloseResults.Subscribe(_ => { HasExecuted = false; });

			// identify button visibility
			this.WhenAny(
				vm => vm.HasExecuted, 
				vm => vm.Game.HasRelease,
				vm => vm.IsExecuting,
				(hasExecuted, hasRelease, isExecuting) => !hasExecuted.Value && !hasRelease.Value && !isExecuting.Value
			).ToProperty(this, vm => vm.ShowIdentifyButton, out _showIdentifyButton);
		}

		public override string ToString()
		{
			return $"[GameViewModel] {Game}";
		}
	}
}
