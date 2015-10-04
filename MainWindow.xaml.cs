using System.Windows;
using PusherClient;
using ReactiveUI;
using VpdbAgent.ViewModels;
using VpdbAgent.Views;
using VpdbAgent.Vpdb;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		public AppViewModel AppViewModel { get; protected set; }

		public MainWindow()
		{
			InitializeComponent();

			AppViewModel = new AppViewModel();
			DataContext = AppViewModel;
		}

		private void SettingsButton_Click(object sender, RoutedEventArgs e)
		{
			AppViewModel.GotoSettings.Execute(null);
		}
	}
}
