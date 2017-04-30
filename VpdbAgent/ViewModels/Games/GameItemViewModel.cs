using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Data;
using VpdbAgent.PinballX;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
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
		private readonly IPinballXManager _pinballXManager;
		private readonly IMessageManager _messageManager;

		// commands
		public ReactiveCommand<Unit, List<VpdbRelease>> IdentifyRelease { get; protected set; }
		public ReactiveCommand<Unit, Unit> AddGame { get; protected set; }
		public ReactiveCommand<Unit, Unit> RemoveGame { get; protected set; }
		public ReactiveCommand<Unit, Unit> HideGame { get; protected set; }
		public ReactiveCommand<Unit, Unit> UnHideGame { get; protected set; }
		public ReactiveCommand<Unit, Unit> DownloadMissing { get; }
		public ReactiveCommand<Unit, Unit> CloseResults { get; }
		public ReactiveCommand<Unit, Unit> SyncToggled { get; }

		// data
		public AggregatedGame Game { get; }

		// needed for filters
		private bool _isVisible = true;
		public bool IsVisible { get { return _isVisible; } set { this.RaiseAndSetIfChanged(ref _isVisible, value); } }

		// release search results
		private IEnumerable<GameResultItemViewModel> _identifiedReleases;
		public IEnumerable<GameResultItemViewModel> IdentifiedReleases { get { return _identifiedReleases; } set { this.RaiseAndSetIfChanged(ref _identifiedReleases, value); } }

		// statuses
		public bool IsExecuting => _isExecuting.Value;
		public bool ShowIdentifyButton => _showIdentifyButton.Value;
		public bool ShowHideButton => _showHideButton.Value;
		public bool ShowUnHideButton => _showUnHideButton.Value;
		public bool ShowAddToDbButton => _showAddToDbButton.Value;
		public bool ShowRemoveFromDbButton => _showRemoveFromDbButton.Value;
		public bool ShowDownloadMissingButton => _showDownloadMissingButton.Value;
		public bool ShowResults { get { return _showResults; } set { this.RaiseAndSetIfChanged(ref _showResults, value); } }
		public bool HasResults { get { return _hasResults; } set { this.RaiseAndSetIfChanged(ref _hasResults, value); } }
		public bool IsDownloading => _isDownloading.Value;
		public bool IsQueued => _isQueued.Value;
		public bool ShowButtons => _showButtons.Value;
		public double DownloadPercent { get { return _downloadPercent; } set { this.RaiseAndSetIfChanged(ref _downloadPercent, value); } }

		private readonly ObservableAsPropertyHelper<bool> _showIdentifyButton;
		private readonly ObservableAsPropertyHelper<bool> _showHideButton;
		private readonly ObservableAsPropertyHelper<bool> _showUnHideButton;
		private readonly ObservableAsPropertyHelper<bool> _showAddToDbButton;
		private readonly ObservableAsPropertyHelper<bool> _showRemoveFromDbButton;
		private readonly ObservableAsPropertyHelper<bool> _showDownloadMissingButton;
		private readonly ObservableAsPropertyHelper<bool> _isExecuting;
		private readonly ObservableAsPropertyHelper<bool> _isDownloading;
		private readonly ObservableAsPropertyHelper<bool> _isQueued;
		private readonly ObservableAsPropertyHelper<bool> _showButtons;
		private bool _showResults;
		private bool _hasResults;
		private double _downloadPercent;

		public GameItemViewModel(AggregatedGame game, IDependencyResolver resolver)
		{
			Game = game;

			_logger = resolver.GetService<ILogger>();
			_vpdbClient = resolver.GetService<IVpdbClient>();
			_gameManager = resolver.GetService<IGameManager>();
			_pinballXManager = resolver.GetService<IPinballXManager>();
			_messageManager = resolver.GetService<IMessageManager>();
			var threadManager = resolver.GetService<IThreadManager>();

			game.WhenAnyValue(g => g.IsDownloading).ToProperty(this, vm => vm.IsDownloading, out _isDownloading);
			game.WhenAnyValue(g => g.IsQueued).ToProperty(this, vm => vm.IsQueued, out _isQueued);

			// release identify
			IdentifyRelease = ReactiveCommand.CreateFromObservable(() => _vpdbClient.Api.GetReleasesBySize(Game.FileSize, MatchThreshold).SubscribeOn(threadManager.WorkerScheduler));
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
					if (game.FileName == vm.TableFile.Reference.Name && game.FileSize == vm.TableFile.Reference.Bytes) {
						numMatches++;
						match = vm;
					}
				}
				_logger.Info("Found {0} identical match(es).", numMatches);

				// if file name and file size are identical, directly match.
				if (numMatches == 1 && match != null) {
					_logger.Info("File name and size are equal to local release, linking.");
					_gameManager.MapGame(match.Game, match.Release, match.TableFile.Reference.Id);
					//_messageManager.LogReleaseLinked(match.Game, match.Release, match.TableFile.Reference.Id);

				} else {
					_logger.Info("View model updated with identified releases.");
					IdentifiedReleases = releases;
					ShowResults = true;
				}
			}, exception => _vpdbClient.HandleApiError(exception, "identifying a game by file size"));

			//var canSync = this.WhenAnyValue(x => x.Game.IsSynced, x => x.Game.HasRelease, (isSynced, hasRelease) => isSynced && hasRelease);
			//var canSync = this.WhenAnyValue(x => x.Game.Mapping.IsSynced, x => x.Game.MappedRelease, (isSynced, rls) => isSynced && rls != null);
			//SyncToggled = ReactiveCommand.Create(() => { _gameManager.Sync(Game); }, canSync);

			// handle errors
			IdentifyRelease.ThrownExceptions.Subscribe(e => { _logger.Error(e, "Error matching game."); });

			// result switch
			IdentifyRelease.Select(r => r.Count > 0).Subscribe(hasResults => { HasResults = hasResults; });

			// add to db button
			AddGame = ReactiveCommand.Create(() => _gameManager.AddGame(Game));

			// remove from db button
			RemoveGame = ReactiveCommand.Create(() => _pinballXManager.RemoveGame(Game.XmlGame));

			// close button
			CloseResults = ReactiveCommand.Create(() => { ShowResults = false; });

			// hide button
			HideGame = ReactiveCommand.Create(() => _gameManager.HideGame(Game));

			// unhide button
			UnHideGame = ReactiveCommand.Create(() => _gameManager.UnHideGame(Game));

			// download button
			DownloadMissing = ReactiveCommand.Create(() => _gameManager.DownloadGame(Game));

			// spinner
			IdentifyRelease.IsExecuting.ToProperty(this, vm => vm.IsExecuting, out _isExecuting);

			// global buttons hide
			this.WhenAnyValue(
				vm => vm.ShowResults, 
				vm => vm.IsExecuting, 
				vm => vm.IsDownloading, 
				vm => vm.IsQueued, 
				(showResults, isExecuting, isDownloading, isQueued) => !showResults && !isExecuting && !isDownloading && !isQueued
			).ToProperty(this, vm => vm.ShowButtons, out _showButtons);

			// identify button visibility
			this.WhenAny(
				vm => vm.Game.HasLocalFile,
				vm => vm.Game.MappedTableFile,
				vm => vm.ShowButtons, 
				(hasLocalFile, mappedFile, showButtons) => hasLocalFile.Value && mappedFile.Value == null && showButtons.Value
			).ToProperty(this, vm => vm.ShowIdentifyButton, out _showIdentifyButton);

			// hide button visibility
			this.WhenAny(
				vm => vm.Game.HasLocalFile,
				vm => vm.Game.HasMapping,
				vm => vm.Game.HasXmlGame,
				vm => vm.ShowButtons,
				(hasLocalFile, hasMapping, hasXmlGame, showButtons) => hasLocalFile.Value && !hasMapping.Value && !hasXmlGame.Value && showButtons.Value
			).ToProperty(this, vm => vm.ShowHideButton, out _showHideButton);
			
			// unhide button visibility
			this.WhenAny(
				vm => vm.Game.Mapping,
				vm => vm.ShowButtons,
				(mapping, showButtons) => mapping.Value != null && mapping.Value.IsHidden && showButtons.Value
			).ToProperty(this, vm => vm.ShowUnHideButton, out _showUnHideButton);

			// add to db button visibility
			this.WhenAny(
				vm => vm.Game.HasLocalFile,
				vm => vm.Game.HasXmlGame,
				vm => vm.Game.MappedTableFile,
				vm => vm.ShowButtons,
				(hasLocalFile, hasXmlGame, mappedFile, showButtons) => hasLocalFile.Value && !hasXmlGame.Value && mappedFile.Value != null && showButtons.Value
			).ToProperty(this, vm => vm.ShowAddToDbButton, out _showAddToDbButton);

			// remove from db button visibility
			this.WhenAny(
				vm => vm.Game.HasLocalFile,
				vm => vm.Game.HasXmlGame,
				vm => vm.ShowButtons,
				(hasLocalFile, hasXmlGame, showButtons) => !hasLocalFile.Value && hasXmlGame.Value && showButtons.Value
			).ToProperty(this, vm => vm.ShowRemoveFromDbButton, out _showRemoveFromDbButton);

			// download button visibility
			this.WhenAny(
				vm => vm.Game.HasLocalFile,
				vm => vm.Game.MappedTableFile,
				vm => vm.ShowButtons,
				(hasLocalFile, mappedFile, showButtons) => !hasLocalFile.Value && mappedFile.Value != null && showButtons.Value
			).ToProperty(this, vm => vm.ShowDownloadMissingButton, out _showDownloadMissingButton);

			// download progress
			this.WhenAnyValue(vm => vm.IsDownloading).Subscribe(isDownloading => {
				if (isDownloading) {
					Game.MappedJob.WhenAnyValue(j => j.TransferPercent)
						.Sample(TimeSpan.FromMilliseconds(300))
						.Where(x => !Game.MappedJob.IsFinished)
						.Subscribe(progress => {
							// on main thread
							System.Windows.Application.Current.Dispatcher.Invoke(() => {
								DownloadPercent = progress;
							});
						});
				}
			});

		}

		public override string ToString()
		{
			return $"[GameViewModel] {Game}";
		}
	}
}
