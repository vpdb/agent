using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Models;
using VpdbAgent.Vpdb;

namespace VpdbAgent.ViewModels
{
	public class MainReleaseResultsViewModel : ReactiveObject
	{
		// deps
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();
		private static readonly IVpdbClient VpdbClient = Locator.CurrentMutable.GetService<IVpdbClient>();

		public Game Game { get; set; }

		// release search results
		ObservableAsPropertyHelper<List<Vpdb.Models.Release>> _identifiedReleases;
		public List<Vpdb.Models.Release> IdentifiedReleases { get { return _identifiedReleases.Value; } }

		public MainReleaseResultsViewModel(Game game, ReactiveCommand<object> identifyCommand) {
			Game = game;

			// release identify
			_identifiedReleases = identifyCommand
				.ObserveOn(RxApp.MainThreadScheduler)
				.SelectMany(async x => await VpdbClient.Api.GetReleasesBySize(Game.FileSize, 512))
				.ToProperty(this, x => x.IdentifiedReleases, new List<Vpdb.Models.Release>());
		}
	}
}
