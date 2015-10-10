using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.ViewModels
{
	public class MainReleaseResultsItemViewModel : ReactiveObject
	{
		public readonly Game Game;
		public readonly Release Release;

		public MainReleaseResultsItemViewModel(Game game, Release release)
		{
			Game = game;
			Release = release;
		}
	}
}
