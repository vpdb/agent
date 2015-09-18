using System;
using System.Collections.Generic;
using System.Windows;
using VpdbAgent.PinballX;
using VpdbAgent.Settings;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		public List<Release> Releases { get; set; }

		public MainWindow()
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

		private void SettingsButton_Click(object sender, RoutedEventArgs e)
		{
			SettingsWindow settings = new SettingsWindow();
			settings.Show();
		}
	}
}
