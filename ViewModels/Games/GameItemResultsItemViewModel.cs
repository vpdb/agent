using System;
using System.Windows.Input;
using ReactiveUI;
using Splat;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.ViewModels.Games
{
	public class GameItemResultsItemViewModel : ReactiveObject
	{
		private static readonly IGameManager GameManager = Locator.CurrentMutable.GetService<IGameManager>();

		// public members
		public readonly Game Game;
		public readonly Release Release;

		// commands
		public ReactiveCommand<object> SelectResult { get; protected set; } = ReactiveCommand.Create();

		public GameItemResultsItemViewModel(Game game, Release release, ICommand closeCommand)
		{
			Game = game;
			Release = release;

			SelectResult.Subscribe(_ =>
			{
				GameManager.LinkRelease(Game, release);
				closeCommand.Execute(null);
			});
		}
	}
}
