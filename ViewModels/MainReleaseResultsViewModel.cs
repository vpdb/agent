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
	public class MainReleaseResultsViewModel : ReactiveObject
	{
		// deps
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();
		private static readonly IVpdbClient VpdbClient = Locator.CurrentMutable.GetService<IVpdbClient>();

		public Game Game { get; set; }

		// release search results
		readonly ObservableAsPropertyHelper<List<Vpdb.Models.Release>> _identifiedReleases;
		public List<Vpdb.Models.Release> IdentifiedReleases => _identifiedReleases.Value;

		public MainReleaseResultsViewModel(Game game, IReactiveCommand<List<Release>> identifyRelease) {
			Game = game;

			identifyRelease.ToProperty(this, vm => vm.IdentifiedReleases, out _identifiedReleases);
			identifyRelease.ThrownExceptions.Subscribe(e => { Logger.Error(e, "Error matching game."); });
		}
	}
}
