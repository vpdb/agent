using System;
using System.Linq;
using System.Windows.Controls;
using NLog;
using ReactiveUI;
using VpdbAgent.Models;
using System.Reactive.Disposables;

namespace VpdbAgent.ViewModels
{
	public class MainViewModel : ReactiveObject, IRoutableViewModel
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// screen
		public IScreen HostScreen { get; protected set; }
		public string UrlPathSegment => "main";

		// data
		public IReactiveDerivedList<Platform> Platforms { get; private set; }
		public IReactiveDerivedList<Game> Games { get; private set; }

		// commands

		// privates
		private readonly ReactiveList<string> _platformFilter = new ReactiveList<string>();

		public MainViewModel(IScreen screen)
		{
			HostScreen = screen;

			var gameManager = GameManager.GetInstance();
			gameManager.Initialize();

			// create platforms
			Platforms = gameManager.Platforms.CreateDerivedCollection(
				platform => platform,
				platform => platform.IsEnabled,
				(x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal)
			);

			// populate filter
			_platformFilter.AddRange(Platforms.Select(p => p.Name));

			// create games
			Games = gameManager.Games.CreateDerivedCollection(
				game => game,
				game => game.Platform.IsEnabled && _platformFilter.Contains(game.Platform.Name),
				(x, y) => string.Compare(x.Id, y.Id, StringComparison.Ordinal)
			);

			Logger.Info("We got {0} platforms and {1} games.", Platforms.Count, Games.Count);

			this.WhenNavigatedTo(() => Bar());
		}

		private IDisposable Bar()
		{
			return Disposable.Create(() => Foo());
		}

		private void Foo()
		{
			if (true) { }
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
			//GameManager.GetInstance().Games.NotifyRepopulated();

		}
	}
}
