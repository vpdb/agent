using System.Windows;
using VpdbAgent.Pages;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{

		public MainWindow()
		{
			InitializeComponent();

			SettingsManager settingsManager = SettingsManager.GetInstance();

			if (settingsManager.IsInitialized()) {
				MainFrame.Navigate(new MainPage());				
			} else {
				MainFrame.Navigate(new SettingsPage());
			}
		}

		private void SettingsButton_Click(object sender, RoutedEventArgs e)
		{
			MainFrame.Navigate(new SettingsPage());
		}
	}
}
