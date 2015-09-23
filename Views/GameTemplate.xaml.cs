using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NLog;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.Views
{
	/// <summary>
	/// Interaction logic for GameTemplate.xaml
	/// </summary>
	public partial class GameTemplate : UserControl
	{
		public static readonly DependencyProperty GameProperty = DependencyProperty.Register("Game", typeof(Game), typeof(GameTemplate), new PropertyMetadata(default(Game), GamePropertyChanged));
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		static void GamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var source = d as GameTemplate;
			source?.Bind();
		}

		public Game Game
		{
			get { return GetValue(GameProperty) as Game; }
			set
			{
				SetValue(GameProperty, value);
			}
		}

		public GameTemplate()
		{
			InitializeComponent();
		}

		private void Bind()
		{
			Filename.Background = Game.Exists ? Brushes.Transparent : Brushes.DarkRed;
			IdentifyButton.IsEnabled = Game.Exists;
		}

		private async void IdentifyButton_Click(object sender, RoutedEventArgs e)
		{
			var client = VpdbClient.GetInstance();
			var releases = await client.Api.GetReleasesBySize(Game.FileSize, 512);

			Logger.Info("Found {0} matches.", releases.Count);
		}
	}
}
