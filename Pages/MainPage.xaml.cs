using System;
using System.Windows.Controls;
using VpdbAgent.Vpdb;
using VpdbAgent.PinballX;
using VpdbAgent.Vpdb.Models;
using System.Collections.Generic;

namespace VpdbAgent.Pages
{
	/// <summary>
	/// Interaction logic for MainPage.xaml
	/// </summary>
	public partial class MainPage : Page
	{

		public List<Release> Releases { get; set; }

		public MainPage()
		{
			InitializeComponent();
			getReleases();
			//getMenu();
		}

		private async void getReleases()
		{

			VpdbClient client = new VpdbClient();

			try {
				Releases = await client.Api.GetReleases();
				ReleaseList.ItemsSource = Releases;
				foreach (Release release in Releases) {
					Console.WriteLine("{0} - {1} ({2})", release.Game.Title, release.Name, release.Id);
				}
			} catch (Exception e) {
				Console.WriteLine("Error retrieving releases: {0}", e.Message);
			}
		}

		private void getMenu()
		{
			MenuManager menuManager = new MenuManager();
			PinballX.Models.Menu menu = menuManager.parseXml();

			Console.WriteLine("Parsed {0} games.", menu.Games.Count);
			foreach (PinballX.Models.Game game in menu.Games) {
				Console.WriteLine("{0} - {1} ({2})", game.Filename, game.Description, game.ReleaseId);
			}
			//menuManager.saveXml(menu);
		}
	}


}
