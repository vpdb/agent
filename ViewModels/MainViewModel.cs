using System;
using System.Collections.Generic;
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
		private readonly IVpdbClient _vpdbClient;

		// data
		public IReactiveDerivedList<Platform> Platforms { get; private set; }
		public ReactiveList<Game> Games { get; private set; } = new ReactiveList<Game>() { ChangeTrackingEnabled = true };

		// commands
		public ReactiveCommand<object> FilterPlatforms { get; protected set; } = ReactiveCommand.Create();

		// privates
		private readonly ReactiveList<string> _platformFilter = new ReactiveList<string>();

		public MainViewModel(IScreen screen, IGameManager gameManager, IVpdbClient vpdbClient)
		{
			HostScreen = screen;

			// do the initialization here
			_gameManager = gameManager.Initialize();
			_vpdbClient = vpdbClient.Initialize();

			// create platforms
			Platforms = _gameManager.Platforms.CreateDerivedCollection(
				platform => platform,
				platform => platform.IsEnabled,
				(x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal)
			);

			// games
			Observable.Merge(_gameManager.Games.Changed, _platformFilter.Changed).Subscribe(_ => {
				using (Games.SuppressChangeNotifications()) {
					Games.Clear();
					Games.AddRange(_gameManager.Games
						.Where(game => game.Platform.IsEnabled && _platformFilter.Contains(game.Platform.Name))
					);
					Games.Sort((x, y) => string.Compare(x.Id, y.Id, StringComparison.OrdinalIgnoreCase));
				}
			});
		
			// populate filter
			using (_platformFilter.SuppressChangeNotifications()) {
				_platformFilter.AddRange(Platforms.Select(p => p.Name));
			};

			Logger.Info("We got {0} platforms and {1} games.", Platforms.Count, _gameManager.Games.Count);
		}

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
