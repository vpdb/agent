using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Data;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.ViewModels.Games
{
	public class GamesViewModel : ReactiveObject
	{
		// dependencies
		private readonly IPlatformManager _platformManager;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// data
		public IReactiveDerivedList<PinballXSystem> Systems { get; }
		public IReactiveDerivedList<GameItemViewModel> Games { get; }

		// commands
		//public ReactiveCommand<Unit, Unit> FilterPlatforms { get; }
		public ReactiveCommand<Unit, Unit> IdentifyAll { get; }

		// privates
		private readonly ReactiveList<string> _systemFilter = new ReactiveList<string>();
		private readonly IReactiveDerivedList<GameItemViewModel> _allGames;

		public GamesViewModel(IDependencyResolver resolver)
		{
			_platformManager = resolver.GetService<IPlatformManager>();
			var menuManager = resolver.GetService<IMenuManager>();
			var gameManager = resolver.GetService<IGameManager>();

			// setup init listener
			//gameManager.Initialized.Subscribe(_ => SetupTracking());

			// create platforms, filtered and sorted
			Systems = menuManager.Systems.CreateDerivedCollection(
				platform => platform,
				platform => platform.Enabled,
				(x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
			);

			// push all games into AllGames as view models and sorted
			_allGames = gameManager.AggregatedGames.CreateDerivedCollection(
				game => new GameItemViewModel(game, resolver) { IsVisible = true /*IsGameVisible(game)*/ },
				gameViewModel => true																 // filter
			);

			// push filtered game view models into Games
			Games = _allGames.CreateDerivedCollection(
				gameViewModel => gameViewModel, 
				gameViewModel => gameViewModel.IsVisible && gameViewModel.Game.Visible,
				(x, y) => string.Compare(Path.GetFileName(x.Game.FileId), Path.GetFileName(y.Game.FileId), StringComparison.OrdinalIgnoreCase)
			);
		}


		/// <summary>
		/// Sets up the triggers that refresh the lists. Run this once
		/// the origial (i.e. non-derived) lists are populated.
		/// </summary>
		private void SetupTracking()
		{
			// update platform filter when platforms change
			Systems.Changed
				.Select(_ => Unit.Default)
				.StartWith(Unit.Default)
				.Subscribe(UpdatePlatforms);
			
			_allGames.ChangeTrackingEnabled = true;

			// just print that we're happy
			_allGames.Changed.Subscribe(_ => {
				Logger.Info("We've got {0} games, {1} in total.", Games.Count, _allGames.Count);
			});

			// update games view models when platform filter changes
			_systemFilter.Changed
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
				_systemFilter.Add(platformName);
			} else {
				_systemFilter.Remove(platformName);
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
		private bool IsGameVisible(AggregatedGame game)
		{
			return game.Visible;
		}

		/// <summary>
		/// Updates the platform filter when platforms change.
		/// </summary>
		/// <param name="args">Change arguments from ReactiveList</param>
		private void UpdatePlatforms(Unit args)
		{
			// populate filter
			using (_systemFilter.SuppressChangeNotifications()) {
				_systemFilter.Clear();
				_systemFilter.AddRange(Systems.Select(p => p.Name));
			}
			Logger.Info("We've got {0} platforms, {2} visible, {1} in total.", Systems.Count, _platformManager.Platforms.Count, _systemFilter.Count);

		}
	}
}
