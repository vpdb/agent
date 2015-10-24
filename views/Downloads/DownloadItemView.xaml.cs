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
				d(this.OneWayBind(ViewModel, vm => vm.Job.Filename, v => v.FileName.Text));
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
