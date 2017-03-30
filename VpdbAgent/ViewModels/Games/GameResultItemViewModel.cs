using System;
using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Data;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.ViewModels.Games
{
	public class GameResultItemViewModel : ReactiveObject
	{
		private static readonly IGameManager GameManager = Locator.CurrentMutable.GetService<IGameManager>();
		private static readonly IMessageManager MessageManager = Locator.CurrentMutable.GetService<IMessageManager>();

		// public members
		public readonly AggregatedGame Game;
		public readonly VpdbRelease Release;
		public readonly VpdbVersion Version;
		public readonly VpdbTableFile TableFile;

		// commands
		public ReactiveCommand<Unit, Unit> SelectResult { get; protected set; }

		public GameResultItemViewModel(AggregatedGame game, VpdbRelease release, VpdbVersion version, VpdbTableFile tableFile, ICommand closeCommand)
		{
			Game = game;
			Version = version;
			Release = release;
			TableFile = tableFile;

			SelectResult = ReactiveCommand.Create(() => {
				//GameManager.LinkRelease(Game, release, tableFile.Reference.Id);
				//MessageManager.LogReleaseLinked(game, release, tableFile.Reference.Id);

				closeCommand.Execute(null);
			});
		}
	}
}
