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
			DataContext = ViewModel; // to avoid binding error messages

			this.WhenActivated(d => {

				// fields
				d(this.Bind(ViewModel, vm => vm.ApiKey, v => v.ApiKey.Text));
				d(this.Bind(ViewModel, vm => vm.Endpoint, v => v.Endpoint.Text));
				d(this.Bind(ViewModel, vm => vm.AuthUser, v => v.AuthUser.Text));
				d(this.Bind(ViewModel, vm => vm.AuthPass, v => v.AuthPass.Text));
				d(this.OneWayBind(ViewModel, vm => vm.PbxFolder, v => v.PbxFolder.Text));

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

				DataContext = ViewModel;
			});
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
