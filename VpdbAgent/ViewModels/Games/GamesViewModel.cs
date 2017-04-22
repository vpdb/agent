using System;
using System.Collections.Generic;
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

namespace VpdbAgent.ViewModels.Games
{
	public class GamesViewModel : ReactiveObject
	{
		// dependencies
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		// data
		public IReactiveDerivedList<SystemItemViewModel> Systems { get; }
		public IReactiveDerivedList<GameItemViewModel> Games { get; }

		// generic filters
		public bool ShowFilesNotInDatabase  { get { return _showFilesNotInDatabase; } private set { this.RaiseAndSetIfChanged(ref _showFilesNotInDatabase, value); } }

		// commands
		//public ReactiveCommand<Unit, Unit> FilterPlatforms { get; }
		public ReactiveCommand<Unit, Unit> IdentifyAll { get; }

		// privates
		private readonly HashSet<string> _systemFilter = new HashSet<string>();
		private readonly Dictionary<PinballXSystem, HashSet<string>> _executableFilter = new Dictionary<PinballXSystem, HashSet<string>>();
		private readonly Dictionary<PinballXSystem, HashSet<string>> _databaseFileFilter = new Dictionary<PinballXSystem, HashSet<string>>();
		private readonly IReactiveDerivedList<GameItemViewModel> _allGameViewModels;

		// watched props
		private bool _showFilesNotInDatabase = true;

		public GamesViewModel(IDependencyResolver resolver)
		{
			var menuManager = resolver.GetService<IPinballXManager>();
			var gameManager = resolver.GetService<IGameManager>();

			// create platforms, filtered and sorted
			Systems = menuManager.Systems.CreateDerivedCollection(
				system => new SystemItemViewModel(this, system),
				system => system.Enabled,
				(x, y) => string.Compare(x.System.Name, y.System.Name, StringComparison.OrdinalIgnoreCase)
			);

			// this is the list we only create once
			_allGameViewModels = gameManager.AggregatedGames.CreateDerivedCollection(
				game => new GameItemViewModel(game, resolver) { IsVisible = IsGameVisible(game) }
			);

			// this is the list that gets filtered
			Games = _allGameViewModels.CreateDerivedCollection(
				gameViewModel => gameViewModel, 
				gameViewModel => gameViewModel.IsVisible,
				(x, y) => string.Compare(Path.GetFileName(x.Game.FileId), Path.GetFileName(y.Game.FileId), StringComparison.OrdinalIgnoreCase)
			);
			_allGameViewModels.ChangeTrackingEnabled = true;

			// update platform filter when platforms change
			Systems.Changed
				.Select(_ => Unit.Default)
				.StartWith(Unit.Default)
				.Subscribe(x => UpdateSystems());

			menuManager.GamesUpdated.Subscribe(x => RefreshGameVisibility());
			this.WhenAnyValue(vm => vm.ShowFilesNotInDatabase).Subscribe(x => RefreshGameVisibility());
		}

		/// <summary>
		/// The click event from the view that toggles a given platform filter.
		/// </summary>
		/// <param name="platformName">Name of the platform that was toggled</param>
		/// <param name="isChecked">True if enabled, false otherwise.</param>
		public void OnPlatformFilterChanged(string platformName, bool isChecked)
		{
			if (isChecked) {
				_systemFilter.Remove(platformName);				
			} else {
				_systemFilter.Add(platformName);
			}
			RefreshGameVisibility();
		}

		public void OnExecutableFilterChanged(PinballXSystem system, string fileName, bool isChecked)
		{
			fileName = fileName == PinballXSystem.DefaultExecutableLabel ? "" : fileName;
			if (isChecked) {
				_executableFilter[system].Remove(fileName);
			} else {
				_executableFilter[system].Add(fileName);
				
			}
			RefreshGameVisibility();
		}

		public void OnDatabaseFileFilterChanged(PinballXSystem system, string fileName, bool isChecked)
		{
			if (isChecked) {
				_databaseFileFilter[system].Remove(fileName);
			} else {
				_databaseFileFilter[system].Add(fileName);
				
			}
			RefreshGameVisibility();
		}

		/// <summary>
		/// Updates the IsVisible flag on all games in order to filter
		/// depending on the selected platforms.
		/// </summary>
		private void RefreshGameVisibility()
		{
			foreach (var gameViewModel in _allGameViewModels) {
				gameViewModel.IsVisible = IsGameVisible(gameViewModel.Game);
			}
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
			if (game.System != null) {
				if (_systemFilter.Contains(game.System.Name)) {
					return false;
				}
				if (game.HasXmlGame && _databaseFileFilter[game.System].Contains(game.XmlGame.DatabaseFile)) {
					return false;
				}
				if (game.HasXmlGame && _executableFilter[game.System].Contains(game.XmlGame.AlternateExe ?? "")) {
					return false;
				}
			}
			if (!game.HasXmlGame && !_showFilesNotInDatabase) {
				return false;
			}

			return true;
		}

		/// <summary>
		/// Updates the platform filter when systems change.
		/// </summary>
		private void UpdateSystems()
		{
			// populate system filter
			var remainingSystemNames = _systemFilter.ToList();
			Systems.ToList().ForEach(vm => {
				if (_systemFilter.Contains(vm.System.Name)) {
					remainingSystemNames.Remove(vm.System.Name);
				}
			});
			remainingSystemNames.ForEach(systemName => _systemFilter.Remove(systemName));

			// populate executable filter
			var remainingSystems = _executableFilter.Keys.ToList();
			Systems.ToList().ForEach(vm => {
				if (_executableFilter.ContainsKey(vm.System)) {
					remainingSystems.Remove(vm.System);
				} else {
					_executableFilter.Add(vm.System, new HashSet<string>());
				}
			});
			remainingSystems.ForEach(system => _executableFilter.Remove(system));

			// populate xml file filter
			remainingSystems = _databaseFileFilter.Keys.ToList();
			Systems.ToList().ForEach(vm => {
				if (_databaseFileFilter.ContainsKey(vm.System)) {
					remainingSystems.Remove(vm.System);
				} else {
					_databaseFileFilter.Add(vm.System, new HashSet<string>());
				}
			});
			remainingSystems.ForEach(system => _databaseFileFilter.Remove(system));

			RefreshGameVisibility();
			Logger.Info("We've got {0} systems.", Systems.Count);
		}
	}
}
