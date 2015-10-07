using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NLog;
using Splat;
using VpdbAgent.Common;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.Controls
{
	/// <summary>
	/// Interaction logic for GameTemplate.xaml
	/// </summary>
	public partial class GameTemplate : UserControl, GameTemplate.IReleaseResult
	{
		public static readonly DependencyProperty GameProperty = DependencyProperty.Register("Game", typeof(Game), typeof(GameTemplate), new PropertyMetadata(default(Game), GamePropertyChanged));
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly IGameManager _gameManager = Locator.Current.GetService<IGameManager>();
		private readonly IVpdbClient _vpdbClient = Locator.Current.GetService<IVpdbClient>();
		private readonly ImageUtils _imageUtils = ImageUtils.GetInstance();

		static void GamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var source = d as GameTemplate;
			source?.Bind();
		}

		public Game Game
		{
			get { return GetValue(GameProperty) as Game; }
			set { SetValue(GameProperty, value); }
		}

		public List<Release> IdentifiedReleases { get; private set; }

		public GameTemplate()
		{
			InitializeComponent();
		}

		private void Bind()
		{

			if (Game.HasRelease) {
				ReleaseNameWrapper.Visibility = Visibility.Visible;
				ReleaseName.Visibility = Visibility.Visible;
				ReleaseName.Text = Game.Release.Name;
				SyncToggle.Visibility = Visibility.Visible;

				Thumb.Visibility = Visibility.Visible;
				_imageUtils.LoadImage(Game.Release.LatestVersion.Thumb.Image.Url, Thumb, Dispatcher);
				IdentifyButton.Visibility = Visibility.Collapsed;

			} else {
				ReleaseNameWrapper.Visibility = Visibility.Collapsed;
				ReleaseName.Visibility = Visibility.Collapsed;
				Thumb.Visibility = Visibility.Collapsed;
				IdentifyButton.Visibility = Visibility.Visible;
				SyncToggle.Visibility = Visibility.Collapsed;
			}
			Filename.Background = Game.Exists ? Brushes.Transparent : Brushes.DarkRed;
			IdentifyButton.IsEnabled = Game.Exists;
		}

		private async void IdentifyButton_Click(object sender, RoutedEventArgs e)
		{
			IdentifyButton.Visibility = Visibility.Collapsed;
			Progress.Visibility = Visibility.Visible;
			Progress.Start();

			var releases = await _vpdbClient.Api.GetReleasesBySize(Game.FileSize, 512);
			Logger.Info("Found {0} matches.", releases.Count);

			Progress.Visibility = Visibility.Collapsed;
			Progress.Stop();
			ExpandIdentifyResult(releases);
		}

		private void ExpandIdentifyResult(List<Release> releases)
		{
			ReleaseIdentifyResultsTemplate identifyResults;
			if (Panel.Children[Panel.Children.Count - 2] is ReleaseIdentifyResultsTemplate) {
				identifyResults = Panel.Children[Panel.Children.Count - 2] as ReleaseIdentifyResultsTemplate;
				identifyResults.Releases = releases;
				Logger.Info("Re-using existing view!");
			} else {
				identifyResults = new ReleaseIdentifyResultsTemplate(releases, this);
				Panel.Children.Insert(Panel.Children.Count - 1, identifyResults);
				Logger.Info("Creating new view");
			}
			identifyResults.IdentifyResults.IsExpanded = true;
		}

		private void CollapseIdentifyResult()
		{
			(Panel.Children[Panel.Children.Count - 2] as ReleaseIdentifyResultsTemplate).IdentifyResults.IsExpanded = false;
		}

		public void OnCanceled()
		{
			IdentifyButton.Visibility = Visibility.Visible;
			CollapseIdentifyResult();
		}

		public void OnResult(Release result)
		{
			_gameManager.LinkRelease(Game, result);
			CollapseIdentifyResult();
			Game.Release = result;
			Bind();
		}


		public interface IReleaseResult
		{
			void OnCanceled();
			void OnResult(Release result);
		}
	}
}
