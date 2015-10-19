using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Controls;
using NLog;
using ReactiveUI;
using VpdbAgent.Models;
using System.Reactive.Disposables;
using Splat;
using System.Reactive.Linq;
using System.Reactive;
using VpdbAgent.Vpdb;
using static System.String;

namespace VpdbAgent.ViewModels
{
	public class MainViewModel : ReactiveObject, IRoutableViewModel
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// screen
		public IScreen HostScreen { get; protected set; }
		public string UrlPathSegment => "main";

		// dependencies
		private readonly IGameManager _gameManager;

		// data
		public IReactiveDerivedList<Platform> Platforms { get; }
		public IReactiveDerivedList<MainGameViewModel> Games { get; }
		private IReactiveDerivedList<MainGameViewModel> AllGames { get; }

		// commands
		public ReactiveCommand<object> FilterPlatforms { get; protected set; } = ReactiveCommand.Create();

		// privates
		private readonly ReactiveList<string> _platformFilter = new ReactiveList<string>();

		public MainViewModel(IScreen screen, IGameManager gameManager, IVpdbClient vpdbClient)
		{
			HostScreen = screen;

			// do the initialization here
			_gameManager = gameManager.Initialize();
			vpdbClient.Initialize();

			// create platforms, filtered and sorted
			Platforms = _gameManager.Platforms.CreateDerivedCollection(
				platform => platform,
				platform => platform.IsEnabled,
				(x, y) => Compare(x.Name, y.Name, StringComparison.Ordinal)
			);

			// push all games into AllGames as view models and sorted
			AllGames = _gameManager.Games.CreateDerivedCollection(
				game => new MainGameViewModel(game),
				gameViewModel => true,
				(x, y) => Compare(x.Game.Id, y.Game.Id, StringComparison.Ordinal)
			);
			AllGames.ChangeTrackingEnabled = true;

			// push filtered game view models into Games
			Games = AllGames.CreateDerivedCollection(gameViewModel => gameViewModel, gameViewModel => gameViewModel.IsVisible);

			// update games view models when platform filter changes
			_platformFilter.Changed.Subscribe(UpdatePlatformFilter);

			// update platform filter when platforms change
			Platforms.Changed.Subscribe(UpdatePlatforms);

			// just print that we're happy
			AllGames.Changed.Subscribe(_ =>
			{
				Logger.Info("We've got {0} games, {1} in total.", Games.Count, AllGames.Count);
			});
		}


		/// <summary>
		/// Updates the IsVisible flag on all games in order to filter
		/// depending on the selected platforms.
		/// </summary>
		/// <param name="args">Change arguments from ReactiveList</param>
		private void UpdatePlatformFilter(NotifyCollectionChangedEventArgs args)
		{
			using (AllGames.SuppressChangeNotifications()) {
				foreach (var gameViewModel in AllGames) {
					gameViewModel.IsVisible =
						gameViewModel.Game.Platform.IsEnabled &&
						_platformFilter.Contains(gameViewModel.Game.Platform.Name);
				}
			}
		}


		/// <summary>
		/// Updates the platform filter when platforms change.
		/// </summary>
		/// <param name="args">Change arguments from ReactiveList</param>
		private void UpdatePlatforms(NotifyCollectionChangedEventArgs args)
		{
			// populate filter
			using (_platformFilter.SuppressChangeNotifications()) {
				_platformFilter.Clear();
				_platformFilter.AddRange(Platforms.Select(p => p.Name));
			};
			Logger.Info("We've got {0} platforms, {2} visible, {1} in total.", Platforms.Count, _gameManager.Platforms.Count, _platformFilter.Count);

		}


		/// <summary>
		/// The click event from the view that toggles a given platform filter.
		/// </summary>
		/// <param name="sender">View of the checkbox</param>
		/// <param name="e"></param>
		public void OnPlatformFilterChanged(object sender, object e)
		{
			var checkbox = (sender as CheckBox);
			if (checkbox == null) {
				return;
			}
			var platformName = checkbox.Tag as string;

			if (checkbox.IsChecked == true) {
				_platformFilter.Add(platformName);
			} else {
				_platformFilter.Remove(platformName);
			}
		}
	}
}
