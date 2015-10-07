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
using VpdbAgent.Models;
using VpdbAgent.Vpdb;

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
		public ReactiveCommand<object> IdentifyRelease { get; protected set; } = ReactiveCommand.Create();

		// data
		public Game Game { get; private set; }

		// spinner
		ObservableAsPropertyHelper<Visibility> _spinnerVisibility;
		public Visibility SpinnerVisibility { get { return _spinnerVisibility.Value; } }

		public MainGameViewModel(Game game)
		{
			Game = game;
			ReleaseResults = new MainReleaseResultsViewModel(game, IdentifyRelease);

			// spinner
			_spinnerVisibility = IdentifyRelease.IsExecuting
				.Select(x => x ? Visibility.Visible : Visibility.Collapsed)
				.ToProperty(this, x => x.SpinnerVisibility, Visibility.Hidden);
		}
	}
}
