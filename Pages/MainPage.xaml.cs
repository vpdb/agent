using NLog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using VpdbAgent.Vpdb;
using PusherClient;
using System;
using System.Windows;
using System.Collections.Generic;

namespace VpdbAgent.Pages
{
	/// <summary>
	/// Interaction logic for MainPage.xaml
	/// </summary>
	public partial class MainPage : Page
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public ICollectionView Platforms { get; private set; }
		public ICollectionView Games { get; private set; }

		private static readonly List<string> _platformFilter = new List<string>();

		public MainPage()
		{
			InitializeComponent();
			DataContext = this;

			var gameManager = GameManager.GetInstance();
			gameManager.Initialize();

			foreach (var platform in gameManager.Platforms) {
				_platformFilter.Add(platform.Name);
			}

			Platforms = CollectionViewSource.GetDefaultView(gameManager.Platforms);
			Platforms.Filter = PlatformFilter;

			Games = CollectionViewSource.GetDefaultView(gameManager.Games);
			Games.Filter = GameFilter;


			// pusher test
			Logger.Info("Setting up pusher...");
			var client = VpdbClient.GetInstance();

			client.Pusher.ConnectionStateChanged += PusherConnectionStateChanged;
			client.Pusher.Error += PusherError;

			var testChannel = client.Pusher.Subscribe("test-channel");
			testChannel.Subscribed += PusherSubscribed;

			// inline binding
			testChannel.Bind("test-message", (dynamic data) =>
			{
				Logger.Info("[{0}]: {1}", data.name, data.message);
			});

			client.Pusher.Connect();
		}

		private void OnPlatformFilterChanged(object sender, RoutedEventArgs e)
		{
			var checkbox = (sender as CheckBox);
			if (checkbox == null) {
				return;
			}
			var platformName = checkbox.Tag as string;

			if (checkbox.IsChecked == true) {
				_platformFilter.Add(platformName);
			} else {
				_platformFilter.Remove(platformName);
			}
			GameManager.GetInstance().Games.NotifyRepopulated();

		}

		private static bool PlatformFilter(object item)
		{
			var platform = item as Models.Platform;
			return platform != null && platform.Enabled;
		}

		private static bool GameFilter(object item)
		{
			var game = item as Models.Game;
			return game != null && game.Platform.Enabled && _platformFilter.Contains(game.Platform.Name);
		}

		#region Pusher
		private static void PusherConnectionStateChanged(object sender, ConnectionState state)
		{
			Logger.Info("Pusher connection {0}", state);
		}

		private static void PusherError(object sender, PusherException error)
		{
			Logger.Error(error, "Pusher error!");
		}

		private static void PusherSubscribed(object sender)
		{
			Logger.Info("Subscribed to channel.");
		}
		#endregion


		/*
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
		}*/
	}
}
