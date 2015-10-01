using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NLog;
using ReactiveUI;
using VpdbAgent.Models;

namespace VpdbAgent.Pages.ViewModels
{
	public class MainViewModel : ReactiveObject
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


		// data
		public IReactiveDerivedList<Platform> Platforms { get; private set; }
		public IReactiveDerivedList<Game> Games { get; private set; }

		// commands

		// privates
		private readonly ReactiveList<string> _platformFilter = new ReactiveList<string>();


		public MainViewModel()
		{
			var gameManager = GameManager.GetInstance();
			gameManager.Initialize();

			Platforms = gameManager.Platforms.CreateDerivedCollection(
				platform => platform,
				platform => platform.Enabled,
				(x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal)
			);

			Games = gameManager.Games.CreateDerivedCollection(
				game => game,
				game => game.Platform.Enabled && _platformFilter.Contains(game.Platform.Name),
				(x, y) => string.Compare(x.Id, y.Id, StringComparison.Ordinal)
			);

			Logger.Info("We got {0} platforms and {1} games.", Platforms.Count, Games.Count);
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
