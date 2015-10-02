using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VpdbAgent.Views
{
	/// <summary>
	/// Interaction logic for SettingsPage.xaml
	/// </summary>
	public partial class SettingsPage : Page
	{

		private SettingsManager settingsManager = SettingsManager.GetInstance();

		public SettingsPage()
		{
			InitializeComponent();

			updateAdvancedOptions();
			ApiKey.Text = settingsManager.ApiKey;
			AuthUser.Text = settingsManager.AuthUser;
			AuthPass.Password = settingsManager.AuthPass;
			ApiEndpoint.Text = settingsManager.Endpoint;
			PinballXFolderLabel.Content = settingsManager.PbxFolder;

			Loaded += (a, b) =>
			{
				CancelButton.Visibility = NavigationService.CanGoBack ? Visibility.Visible : Visibility.Hidden;
			};
		}

		
		private void PinballXFolderButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new FolderBrowserDialog();
			dialog.ShowNewFolderButton = false;
			if (PinballXFolderLabel.Content.ToString().Length > 0) {
				dialog.SelectedPath = PinballXFolderLabel.Content.ToString();
			}
			DialogResult result = dialog.ShowDialog();

			if (result == DialogResult.OK) {
				PinballXFolderLabel.Content = dialog.SelectedPath;

			} else {
				PinballXFolderLabel.Content = string.Empty;
			}
		}

		private void SubmitButton_Click(object sender, RoutedEventArgs e)
		{

			settingsManager.ApiKey = ApiKey.Text;
			settingsManager.AuthUser = AuthUser.Text;
			settingsManager.AuthPass = AuthPass.Password;
			settingsManager.Endpoint = ApiEndpoint.Text;
			settingsManager.PbxFolder = PinballXFolderLabel.Content.ToString();

			Dictionary<string, string> errors = settingsManager.Validate();
			if (errors.Count == 0) {
				settingsManager.Save();

				if (NavigationService.CanGoBack) {
					NavigationService.GoBack();
				} else {
					NavigationService.Navigate(new MainView());
					NavigationService.RemoveBackEntry();
				}
				
			} else {
				// TODO properly display error
			}
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
