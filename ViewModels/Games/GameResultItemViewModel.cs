using System;
using System.Windows.Input;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.ViewModels.Games
{
	public class GameResultItemViewModel : ReactiveObject
	{
		private static readonly IGameManager GameManager = Locator.CurrentMutable.GetService<IGameManager>();
		private static readonly IMessageManager MessageManager = Locator.CurrentMutable.GetService<IMessageManager>();

		// public members
		public readonly Game Game;
		public readonly VpdbRelease Release;
		public readonly VpdbVersion Version;
		public readonly VpdbTableFile TableFile;

		// commands
		public ReactiveCommand<object> SelectResult { get; protected set; } = ReactiveCommand.Create();

		public GameResultItemViewModel(Game game, VpdbRelease release, VpdbVersion version, VpdbTableFile tableFile, ICommand closeCommand)
		{
			Game = game;
			Version = version;
			Release = release;
			TableFile = tableFile;

			SelectResult.Subscribe(_ =>
			{
				GameManager.LinkRelease(Game, release, tableFile.Reference.Id);
				MessageManager.LogReleaseLinked(game, release, tableFile.Reference.Id);

				closeCommand.Execute(null);
			});
		}
	}
}
