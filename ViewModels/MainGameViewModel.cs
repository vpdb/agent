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
		readonly ObservableAsPropertyHelper<Visibility> _spinnerVisibility;
		public Visibility SpinnerVisibility => _spinnerVisibility.Value;


		public MainGameViewModel(Game game)
		{
			Game = game;

			// release identify
			IdentifyRelease = ReactiveCommand.CreateAsyncTask(async _ => await VpdbClient.Api.GetReleasesBySize(game.FileSize, 512));

			// spinner
			_spinnerVisibility = IdentifyRelease.IsExecuting
				.Select(executing => executing ? Visibility.Visible : Visibility.Collapsed)
				.ToProperty(this, x => x.SpinnerVisibility, Visibility.Hidden);

			// inner views
			ReleaseResults = new MainReleaseResultsViewModel(game, IdentifyRelease);
		}
	}
}
