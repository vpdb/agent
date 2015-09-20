using System;
using System.Windows.Controls;
using VpdbAgent.Vpdb;
using VpdbAgent.PinballX;
using VpdbAgent.Vpdb.Models;
using System.Collections.Generic;
using System.Linq;

namespace VpdbAgent.Pages
{
	/// <summary>
	/// Interaction logic for MainPage.xaml
	/// </summary>
	public partial class MainPage : Page
	{

		public List<PinballX.Models.Game> Games { get; set; }
		public List<Release> Releases { get; set; }
		public MenuManager MenuManager { get; set; }

		public MainPage()
		{
			MenuManager = new MenuManager();
			InitializeComponent();
			//getReleases();
			Systems.ItemsSource = MenuManager.Systems.Where(p => (p.Enabled == true));
			foreach (PinballXSystem system in MenuManager.Systems) {
				Console.WriteLine("+++ System {0} - {1}", system.Name, system.Enabled);
			}
			Games = MenuManager.GetGames();
			GamesList.ItemsSource = Games;

			GameManager gameManager = new GameManager();
		}

		private async void getReleases()
		{

			VpdbClient client = new VpdbClient();

			try {
				Releases = await client.Api.GetReleases();
				GamesList.ItemsSource = Releases;
				foreach (Release release in Releases) {
					Console.WriteLine("{0} - {1} ({2})", release.Game.Title, release.Name, release.Id);
				}
			} catch (Exception e) {
				Console.WriteLine("Error retrieving releases: {0}", e.Message);
			}
		}
	}
}
