using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.ViewModels
{
	public class MainGameViewModel : ReactiveObject
	{
		// deps
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();
		private static readonly IVpdbClient VpdbClient = Locator.CurrentMutable.GetService<IVpdbClient>();

		// children
		public MainReleaseResultsViewModel ReleaseResults;

		// commands
		public ReactiveCommand<List<Release>> IdentifyRelease { get; protected set; }

		// data
		public Game Game { get; private set; }

		// spinner
		private readonly ObservableAsPropertyHelper<bool> _isExecuting;
		public bool IsExecuting => _isExecuting.Value;

		private readonly ObservableAsPropertyHelper<bool> _hasExecuted;
		public bool HasExecuted => _hasExecuted.Value;


		public MainGameViewModel(Game game)
		{
			Game = game;

			// release identify
			IdentifyRelease = ReactiveCommand.CreateAsyncTask(async _ => await VpdbClient.Api.GetReleasesBySize(game.FileSize, 512));

			// spinner
			_isExecuting = IdentifyRelease.IsExecuting.ToProperty(this, vm => vm.IsExecuting, out _isExecuting);

			// inner views
			IdentifyRelease.IsExecuting
				.Skip(1)         // skip initial false value
				.Where(x => !x)  // then trigger when false again
				.Select(_ => true)
				.ToProperty(this, vm => vm.HasExecuted, out _hasExecuted);

			// children
			ReleaseResults = new MainReleaseResultsViewModel(game, IdentifyRelease);

		}
	}
}
