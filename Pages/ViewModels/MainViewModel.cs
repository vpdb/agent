using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VpdbAgent.Models;

namespace VpdbAgent.Pages.ViewModels
{
	public class MainViewModel : ReactiveObject
	{

		public IReactiveDerivedList<Platform> Platforms { get; private set; }
		public ReactiveList<Game> Games { get; private set; }

		public MainViewModel()
		{
			var gameManager = GameManager.GetInstance();
			gameManager.Initialize();

			Platforms = gameManager.Platforms;
			Games = gameManager.Games;

			Console.WriteLine("We got {0} platforms and {1} games.", Platforms.Count, Games.Count);
		}
	}
}
