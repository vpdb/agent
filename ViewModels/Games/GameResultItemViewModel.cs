using System;
using System.Windows.Input;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;
using Version = VpdbAgent.Vpdb.Models.Version;

namespace VpdbAgent.ViewModels.Games
{
	public class GameResultItemViewModel : ReactiveObject
	{
		private static readonly IGameManager GameManager = Locator.CurrentMutable.GetService<IGameManager>();

		// public members
		public readonly Game Game;
		public readonly Release Release;
		public readonly Version Version;
		public readonly File File;

		// commands
		public ReactiveCommand<object> SelectResult { get; protected set; } = ReactiveCommand.Create();

		public GameResultItemViewModel(Game game, Release release, Version version, File file, ICommand closeCommand)
		{
			Game = game;
			Version = version;
			Release = release;
			File = file;

			SelectResult.Subscribe(_ =>
			{
				GameManager.LinkRelease(Game, release, file.Reference.Id);
				closeCommand.Execute(null);
			});
		}
	}
}
