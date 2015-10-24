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
using VpdbAgent.ViewModels.Downloads;

namespace VpdbAgent.Views.Downloads
{
	/// <summary>
	/// Interaction logic for DownloadItemView.xaml
	/// </summary>
	public partial class DownloadItemView : UserControl, IViewFor<DownloadItemViewModel>
	{
		public DownloadItemView()
		{
			InitializeComponent();

			this.WhenActivated(d => {
				d(this.OneWayBind(ViewModel, vm => vm.Job.Release.Game.DisplayName, v => v.GameName.Text));
				d(this.OneWayBind(ViewModel, vm => vm.Job.Release.Name, v => v.ReleaseName.Text));
				d(this.OneWayBind(ViewModel, vm => vm.Job.Version.Name, v => v.ReleaseVersion.Text));
				d(this.OneWayBind(ViewModel, vm => vm.Job.Filename, v => v.FileName.Text));
				d(this.OneWayBind(ViewModel, vm => vm.Job.File.Media["playfield_image"].Variations["square"].Url, v => v.Thumb.UrlSource));

				d(this.OneWayBind(ViewModel, vm => vm.Job.DownloadSpeedFormatted, v => v.DownloadSpeed.Content));
				d(this.OneWayBind(ViewModel, vm => vm.Job.DownloadSizeFormatted, v => v.DownloadSize.Text));
				d(this.OneWayBind(ViewModel, vm => vm.Job.DownloadPercentFormatted, v => v.DownloadPercent.Text));
				d(this.OneWayBind(ViewModel, vm => vm.Job.DownloadPercent, v => v.ProgressBar.Value));
			});
		}

		#region ViewModel
		public DownloadItemViewModel ViewModel
		{
			get { return (DownloadItemViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(DownloadItemViewModel), typeof(DownloadItemView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (DownloadItemViewModel)value; }
		}
		#endregion
	}
}
