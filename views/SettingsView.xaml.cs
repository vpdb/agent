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
using ReactiveUI;
using VpdbAgent.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace VpdbAgent.Views
{
	/// <summary>
	/// Interaction logic for SettingsPage.xaml
	/// </summary>
	public partial class SettingsView : UserControl, IViewFor<SettingsViewModel>
	{

		public SettingsView()
		{
			InitializeComponent();
			UpdateAdvancedOptions();

			// model
			this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.DataContext);

			// fields
			this.Bind(ViewModel, vm => vm.ApiKey, v => v.ApiKey.Text);
			this.Bind(ViewModel, vm => vm.Endpoint, v => v.ApiEndpoint.Text);
			this.Bind(ViewModel, vm => vm.PbxFolder, v => v.PbxFolder.Content);
			this.Bind(ViewModel, vm => vm.AuthUser, v => v.AuthUser.Text);
			this.Bind(ViewModel, vm => vm.AuthPass, v => v.AuthPass.Password);

			// commands
			this.BindCommand(ViewModel, vm => vm.CloseSettings, v => v.CancelButton);
			this.BindCommand(ViewModel, vm => vm.SaveSettings, v => v.SaveButton);

			//CancelButton.Visibility = NavigationService.CanGoBack ? Visibility.Visible : Visibility.Hidden;
		}


		private void PinballXFolderButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new FolderBrowserDialog {
				ShowNewFolderButton = false
			};

			if (PbxFolder.Content.ToString().Length > 0) {
				dialog.SelectedPath = PbxFolder.Content.ToString();
			}

			var result = dialog.ShowDialog();
			PbxFolder.Content = result == DialogResult.OK ? dialog.SelectedPath : string.Empty;
		}

		private void ShowAdvancedOptions_Checked(object sender, RoutedEventArgs e)
		{
			UpdateAdvancedOptions();
		}

		private void UpdateAdvancedOptions()
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

		#region ViewModel
		public SettingsViewModel ViewModel
		{
			get { return (SettingsViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(SettingsViewModel), typeof(SettingsView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (SettingsViewModel)value; }
		}
		#endregion

	}
}
