using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Common.Extensions;
using VpdbAgent.Data.Objects;
using Game = VpdbAgent.Models.Game;
using Platform = VpdbAgent.Models.Platform;

namespace VpdbAgent.ViewModels.Games
{
	public class GamesViewModel : ReactiveObject
	{
		// dependencies
		private readonly IPlatformManager _platformManager;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// data
		public IReactiveDerivedList<Platform> Platforms { get; }
		public IReactiveDerivedList<GameItemViewModel> Games { get; }

		// commands
		//public ReactiveCommand<Unit, Unit> FilterPlatforms { get; }
		public ReactiveCommand<Unit, Unit> IdentifyAll { get; }

		// privates
		private readonly ReactiveList<string> _platformFilter = new ReactiveList<string>();
		private readonly IReactiveDerivedList<GameItemViewModel> _allGames;

		public GamesViewModel(IDependencyResolver resolver)
		{
			_platformManager = resolver.GetService<IPlatformManager>();
			var gameManager = resolver.GetService<IGameManager>();

			// setup init listener
			gameManager.Initialized.Subscribe(_ => SetupTracking());

			// create platforms, filtered and sorted
			Platforms = _platformManager.Platforms.CreateDerivedCollection(
				platform => platform,
				platform => platform.IsEnabled,
				(x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal)
			);

			// push all games into AllGames as view models and sorted
			_allGames = gameManager.AggregatedGames.CreateDerivedCollection(
				game => new GameItemViewModel(game, resolver) { IsVisible = true /*IsGameVisible(game)*/ },
				gameViewModel => true,																 // filter
				(x, y) => string.Compare(x.Game.FileName, y.Game.FileName, StringComparison.Ordinal) // order
			);

			// push filtered game view models into Games
			Games = _allGames.CreateDerivedCollection(
				gameViewModel => gameViewModel, 
				gameViewModel => gameViewModel.IsVisible);


			// todo check if we can simplify this.
			/*
			IdentifyAll = ReactiveCommand.Create(() => {

				/*
				Games
					.ToObservable()
					.Where(g => g.ShowIdentifyButton)
					.StepInterval(TimeSpan.FromMilliseconds(200)) // don't DOS the server...
					.Select(g => Observable.DeferAsync(async token =>
						Observable.Return(await System.Windows.Application.Current. // must be on main thread
							Dispatcher.Invoke(async () => new { game = g, result = await g.IdentifyRelease.ExecuteAsyncTask() }))))
					.Merge(1)
					.Subscribe(x => {
						if (x.result.Count == 0) {
							System.Windows.Application.Current.Dispatcher.Invoke(delegate {
								x.game.CloseResults.Execute(null);
							});
						}
					});*

				Games
					.ToObservable()
					.Where(g => !g.HasExecuted && !g.Game.HasRelease && !g.IsExecuting)
					.StepInterval(TimeSpan.FromMilliseconds(200)) // don't DOS the server...
					.Subscribe(g => {
						System.Windows.Application.Current.Dispatcher.Invoke(() => {
							g.IdentifyRelease.Execute().Subscribe(result => {
								if (result.Count == 0) {
									g.CloseResults.Execute();
								}
							});
						});
					});
			});*/
		}


		/// <summary>
		/// Sets up the triggers that refresh the lists. Run this once
		/// the origial (i.e. non-derived) lists are populated.
		/// </summary>
		private void SetupTracking()
		{
			// update platform filter when platforms change
			Platforms.Changed
				.Select(_ => Unit.Default)
				.StartWith(Unit.Default)
				.Subscribe(UpdatePlatforms);
			
			_allGames.ChangeTrackingEnabled = true;

			// just print that we're happy
			_allGames.Changed.Subscribe(_ => {
				Logger.Info("We've got {0} games, {1} in total.", Games.Count, _allGames.Count);
			});

			// update games view models when platform filter changes
			_platformFilter.Changed
				.Select(_ => Unit.Default)
				.StartWith(Unit.Default)
				.Subscribe(RefreshGameVisibility);
		}

		/// <summary>
		/// The click event from the view that toggles a given platform filter.
		/// </summary>
		/// <param name="platformName">Name of the platform that was toggled</param>
		/// <param name="isChecked">True if enabled, false otherwise.</param>
		public void OnPlatformFilterChanged(string platformName, bool isChecked)
		{
			if (isChecked) {
				_platformFilter.Add(platformName);
			} else {
				_platformFilter.Remove(platformName);
			}
		}

		/// <summary>
		/// Updates the IsVisible flag on all games in order to filter
		/// depending on the selected platforms.
		/// </summary>
		/// <param name="args">Change arguments from ReactiveList</param>
		private void RefreshGameVisibility(Unit args)
		{/*
			using (_allGames.SuppressChangeNotifications()) {
				foreach (var gameViewModel in _allGames) {
					gameViewModel.IsVisible = IsGameVisible(gameViewModel.Game);
				}
			}*/
		}

		/// <summary>
		/// Returns true if a given game should be displayed or false otherwise.
		/// 
		/// Visibility is determined in function of platform filters and enabled
		/// platforms.
		/// </summary>
		/// <param name="game">Game</param>
		/// <returns>True if visible, false otherwise</returns>
		private bool IsGameVisible(Game game)
		{
			return game.Platform.IsEnabled && _platformFilter.Contains(game.Platform.Name);
		}

		private bool IsGameVisible(AggregatedGame game)
		{
			return game.Enabled;
		}

		/// <summary>
		/// Updates the platform filter when platforms change.
		/// </summary>
		/// <param name="args">Change arguments from ReactiveList</param>
		private void UpdatePlatforms(Unit args)
		{
			// populate filter
			using (_platformFilter.SuppressChangeNotifications()) {
				_platformFilter.Clear();
				_platformFilter.AddRange(Platforms.Select(p => p.Name));
			}
			Logger.Info("We've got {0} platforms, {2} visible, {1} in total.", Platforms.Count, _platformManager.Platforms.Count, _platformFilter.Count);

		}
	}
}
