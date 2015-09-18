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
			MainFrame.Navigate(new MainPage());
		}

		private void SettingsButton_Click(object sender, RoutedEventArgs e)
		{
			MainFrame.Navigate(new SettingsPage());
		}
	}
}
