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
using ReactiveUI;
using VpdbAgent.ViewModels;

namespace VpdbAgent.Views
{
	/// <summary>
	/// Interaction logic for MainView.xaml
	/// </summary>
	public partial class MainView : UserControl, IViewFor<MainViewModel>
	{
		public MainView()
		{
			InitializeComponent();

			this.WhenActivated(d => {

				// tab content
				d(this.OneWayBind(ViewModel, vm => vm.Games, v => v.GamesContent.ViewModel));
				d(this.OneWayBind(ViewModel, vm => vm.Downloads, v => v.DownloadsContent.ViewModel));
				//d(this.OneWayBind(ViewModel, vm => vm.Messsages, v => v.MessagesContent.ViewModel));

				// status fields
				d(this.OneWayBind(ViewModel, vm => vm.LoginStatus, v => v.LoginStatus.Text));
				d(this.OneWayBind(ViewModel, vm => vm.VersionName, v => v.AppVersion.Text));
				d(this.OneWayBind(ViewModel, vm => vm.ShowUpdateNotice, v => v.UpdateNotice.Visibility));

				// commands
				d(this.BindCommand(ViewModel, vm => vm.GotoSettings, v => v.GotoSettings));
				d(this.BindCommand(ViewModel, vm => vm.RestartApp, v => v.RestartApp));
				d(this.BindCommand(ViewModel, vm => vm.CloseUpdateNotice, v => v.CloseUpdateNotice));

			});
		}

		#region ViewModel
		public MainViewModel ViewModel
		{
			get { return (MainViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(MainViewModel), typeof(MainView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (MainViewModel)value; }
		}
		#endregion
	}
}
