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
using VpdbAgent.ViewModels.Settings;
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

			this.WhenActivated(d => {

				// fields
				d(this.Bind(ViewModel, vm => vm.ApiKey, v => v.ApiKey.Text));
				d(this.Bind(ViewModel, vm => vm.Endpoint, v => v.Endpoint.Text));
				d(this.Bind(ViewModel, vm => vm.AuthUser, v => v.AuthUser.Text));
				d(this.Bind(ViewModel, vm => vm.AuthPass, v => v.AuthPass.Text));
				d(this.OneWayBind(ViewModel, vm => vm.PbxFolder, v => v.PbxFolder.Text));
				d(this.OneWayBind(ViewModel, vm => vm.PbxFolderLabel, v => v.PbxFolderLabel.Text));

				// advanced options
				d(this.Bind(ViewModel, vm => vm.ShowAdvancedOptions, v => v.ShowAdvancedOptions.IsChecked));
				d(this.OneWayBind(ViewModel, vm => vm.ShowAdvancedOptions, v => v.ApiEndpointLabel.Visibility));
				d(this.OneWayBind(ViewModel, vm => vm.ShowAdvancedOptions, v => v.Endpoint.Visibility));
				d(this.OneWayBind(ViewModel, vm => vm.ShowAdvancedOptions, v => v.BasicAuthLabel.Visibility));
				d(this.OneWayBind(ViewModel, vm => vm.ShowAdvancedOptions, v => v.BasicAuth.Visibility));

				// error fields
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.PbxFolderErrorPanel.Visibility, null, "PbxFolder"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.PbxFolderError.Content, null, "PbxFolder"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.ApiKeyErrorPanel.Visibility, null, "ApiKey"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.ApiKeyError.Content, null, "ApiKey"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.AuthErrorPanel.Visibility, null, "Auth"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.AuthError.Content, null, "Auth"));

				// commands
				d(this.BindCommand(ViewModel, vm => vm.ChooseFolder, v => v.PinballXFolderButton));
				d(this.BindCommand(ViewModel, vm => vm.CloseSettings, v => v.CancelButton));
				d(this.BindCommand(ViewModel, vm => vm.SaveSettings, v => v.SaveButton));
			});
		
			//CancelButton.Visibility = NavigationService.CanGoBack ? Visibility.Visible : Visibility.Hidden;
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
