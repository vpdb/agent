using System.Windows;
using ReactiveUI;
using VpdbAgent.ViewModels.Settings;
using UserControl = System.Windows.Controls.UserControl;

namespace VpdbAgent.Views.Settings
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

				DataContext = ViewModel;

				// fields
				d(this.Bind(ViewModel, vm => vm.ApiKey, v => v.ApiKey.Text));
				d(this.Bind(ViewModel, vm => vm.Endpoint, v => v.Endpoint.Text));
				d(this.Bind(ViewModel, vm => vm.AuthUser, v => v.AuthUser.Text));
				d(this.Bind(ViewModel, vm => vm.AuthPass, v => v.AuthPass.Text));
				d(this.OneWayBind(ViewModel, vm => vm.PbxFolder, v => v.PbxFolder.Text));
				d(this.OneWayBind(ViewModel, vm => vm.PbxFolderLabel, v => v.PbxFolderLabel.Text));

				// error fields
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.PbxFolderErrorPanel.Visibility, null, "PbxFolder"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.PbxFolderError.Text, null, "PbxFolder"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.ApiKeyErrorPanel.Visibility, null, "ApiKey"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.ApiKeyError.Text, null, "ApiKey"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.AuthErrorPanel.Visibility, null, "Auth"));
				d(this.OneWayBind(ViewModel, vm => vm.Errors, v => v.AuthError.Text, null, "Auth"));

				// commands
				d(this.BindCommand(ViewModel, vm => vm.ChooseFolder, v => v.PinballXFolderButton));
				d(this.BindCommand(ViewModel, vm => vm.CloseSettings, v => v.CancelButton));
				d(this.BindCommand(ViewModel, vm => vm.SaveSettings, v => v.SaveButton));

				//d(this.OneWayBind(ViewModel, vm => vm.IsValidating, v => v.ProgressSpinner.IsVisible));
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
