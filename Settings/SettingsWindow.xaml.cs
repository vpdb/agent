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
using System.Windows.Shapes;

namespace VpdbAgent.Settings
{
	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow 
	{
		public SettingsWindow()
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
			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
