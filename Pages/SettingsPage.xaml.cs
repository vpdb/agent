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

namespace VpdbAgent.Pages
{
	/// <summary>
	/// Interaction logic for SettingsPage.xaml
	/// </summary>
	public partial class SettingsPage : Page
	{

		public SettingsPage()
		{
			InitializeComponent();

			updateAdvancedOptions();
			ApiKey.Text = (string)Properties.Settings.Default["ApiKey"];
			AuthUser.Text = (string)Properties.Settings.Default["AuthUser"];
			AuthPass.Password = (string)Properties.Settings.Default["AuthPass"];
			ApiEndpoint.Text = (string)Properties.Settings.Default["Endpoint"];
		}

		private void SubmitButton_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["ApiKey"] = ApiKey.Text;
			Properties.Settings.Default["AuthUser"] = AuthUser.Text;
			Properties.Settings.Default["AuthPass"] = AuthPass.Password;
			Properties.Settings.Default["Endpoint"] = ApiEndpoint.Text;
			Properties.Settings.Default.Save();
			NavigationService.GoBack();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			NavigationService.GoBack();
		}

		private void ShowAdvancedOptions_Checked(object sender, RoutedEventArgs e)
		{
			updateAdvancedOptions();
		}

		private void updateAdvancedOptions()
		{
			if (!(bool)ShowAdvancedOptions.IsChecked) {
				ApiEndpointLabel.Visibility = Visibility.Hidden;
				ApiEndpoint.Visibility = Visibility.Hidden;
				BasicAuthLabel.Visibility = Visibility.Hidden;
				BasicAuth.Visibility = Visibility.Hidden;
			} else {
				ApiEndpointLabel.Visibility = Visibility.Visible;
				ApiEndpoint.Visibility = Visibility.Visible;
				BasicAuthLabel.Visibility = Visibility.Visible;
				BasicAuth.Visibility = Visibility.Visible;
			}
		}
	}
}
