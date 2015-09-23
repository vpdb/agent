using NLog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using VpdbAgent.Vpdb;
using PusherClient;

namespace VpdbAgent.Pages
{
	/// <summary>
	/// Interaction logic for MainPage.xaml
	/// </summary>
	public partial class MainPage : Page
	{
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		public ICollectionView Platforms { get; private set; }
		public ICollectionView Games { get; private set; }

		public MainPage()
		{
			InitializeComponent();
			DataContext = this;

			GameManager gameManager = GameManager.GetInstance();
			gameManager.Initialize();

			Platforms = CollectionViewSource.GetDefaultView(gameManager.Platforms);
			Platforms.Filter = PlatformFilter;

			Games = CollectionViewSource.GetDefaultView(gameManager.Games);
			Games.Filter = GameFilter;

			logger.Info("Setting up pusher...");
			VpdbClient client = new VpdbClient();
			
			client.Pusher.ConnectionStateChanged += PusherConnectionStateChanged;
			client.Pusher.Error += PusherError;

			Channel testChannel = client.Pusher.Subscribe("test-channel");
			testChannel.Subscribed += PusherSubscribed;

			// inline binding
			testChannel.Bind("test-message", (dynamic data) => {
				logger.Info("[{0}]: {1}", data.name, data.message);
			});

			client.Pusher.Connect();
		}

		private bool PlatformFilter(object item)
		{
			Models.Platform platform = item as Models.Platform;
			return platform.Enabled;
		}

		private bool GameFilter(object item)
		{
			Models.Game game = item as Models.Game;
			return game.Platform.Enabled;
		}

		private void PusherConnectionStateChanged(object sender, ConnectionState state)
		{
			logger.Info("Pusher connection {0}", state);
		}

		private void PusherError(object sender, PusherException error) {
			logger.Error(error, "Pusher error!");
		}

		private void PusherSubscribed(object sender)
		{
			logger.Info("Subscribed to channel.");
		}


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
