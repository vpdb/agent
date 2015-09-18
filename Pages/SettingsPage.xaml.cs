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

			ApiKey.Text = (string)Properties.Settings.Default["ApiKey"];
			AuthUser.Text = (string)Properties.Settings.Default["AuthUser"];
			AuthPass.Password = (string)Properties.Settings.Default["AuthPass"];
		}

		private void SubmitButton_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["ApiKey"] = ApiKey.Text;
			Properties.Settings.Default["AuthUser"] = AuthUser.Text;
			Properties.Settings.Default["AuthPass"] = AuthPass.Password;
			Properties.Settings.Default.Save();
			NavigationService.GoBack();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			NavigationService.GoBack();
		}
	}
}
