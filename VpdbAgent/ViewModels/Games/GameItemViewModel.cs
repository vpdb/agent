using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;
using ILogger = NLog.ILogger;

namespace VpdbAgent.ViewModels.Games
{
	public class GameItemViewModel : ReactiveObject
	{
		public const long MatchThreshold = 262144;

		// deps
		private readonly ILogger _logger;
		private readonly IVpdbClient _vpdbClient;
		private readonly IGameManager _gameManager;
		private readonly IMessageManager _messageManager;

		// commands
		public ReactiveCommand<List<VpdbRelease>> IdentifyRelease { get; protected set; }
		public ReactiveCommand<object> CloseResults { get; } = ReactiveCommand.Create();
		public ReactiveCommand<object> SyncToggled { get; } = ReactiveCommand.Create();

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
		public bool ShowIdentifyButton => _showIdentifyButton.Value;
		public bool HasExecuted { get { return _hasExecuted; } set { this.RaiseAndSetIfChanged(ref _hasExecuted, value); } }
		public bool HasResults { get { return _hasResults; } set { this.RaiseAndSetIfChanged(ref _hasResults, value); } }

		private readonly ObservableAsPropertyHelper<bool> _showIdentifyButton;
		private readonly ObservableAsPropertyHelper<bool> _isExecuting;
		private bool _hasExecuted;
		private bool _hasResults;

		public GameItemViewModel(Game game, IDependencyResolver resolver)
		{
			Game = game;

			_logger = resolver.GetService<ILogger>();
			_vpdbClient = resolver.GetService<IVpdbClient>();
			_gameManager = resolver.GetService<IGameManager>();
			_messageManager = resolver.GetService<IMessageManager>();
			var threadManager = resolver.GetService<IThreadManager>();

			// release identify
			IdentifyRelease = ReactiveCommand.CreateAsyncObservable(_ => _vpdbClient.Api.GetReleasesBySize(Game.FileSize, MatchThreshold).SubscribeOn(threadManager.WorkerScheduler));
			IdentifyRelease.Select(releases => releases
				.Select(release => new {release, release.Versions})
				.SelectMany(x => x.Versions.Select(version => new {x.release, version, version.Files}))
				.SelectMany(x => x.Files.Select(file => new GameResultItemViewModel(game, x.release, x.version, file, CloseResults)))
			).Subscribe(x => {

				var releases = x as GameResultItemViewModel[] ?? x.ToArray();
				var numMatches = 0;
				_logger.Info("Found {0} releases for game to identify.", releases.Length);
				GameResultItemViewModel match = null;
				foreach (var vm in releases) {
					if (game.Filename == vm.TableFile.Reference.Name && game.FileSize == vm.TableFile.Reference.Bytes) {
						numMatches++;
						match = vm;
					}
				}
				_logger.Info("Found {0} identical match(es).", numMatches);

				// if file name and file size are identical, directly match.
				if (numMatches == 1 && match != null) {
					_logger.Info("File name and size are equal to local release, linking.");
					_gameManager.LinkRelease(match.Game, match.Release, match.TableFile.Reference.Id);
					_messageManager.LogReleaseLinked(match.Game, match.Release, match.TableFile.Reference.Id);

				} else {
					_logger.Info("View model updated with identified releases.");
					IdentifiedReleases = releases;
					HasExecuted = true;
				}
			}, exception => _vpdbClient.HandleApiError(exception, "identifying a game by file size"));

			//SyncToggled
			//	.Where(_ => Game.IsSynced && Game.HasRelease)
			//	.Subscribe(_ => { GameManager.Sync(Game); });

			// handle errors
			IdentifyRelease.ThrownExceptions.Subscribe(e => { _logger.Error(e, "Error matching game."); });

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
