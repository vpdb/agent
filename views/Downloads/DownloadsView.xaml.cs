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
	/// Interaction logic for DownloadsView.xaml
	/// </summary>
	public partial class DownloadsView : UserControl, IViewFor<DownloadsViewModel>
	{
		public DownloadsView()
		{
			InitializeComponent();

			this.WhenActivated(d => {
				d(this.OneWayBind(ViewModel, vm => vm.Jobs, v => v.DownloadList.ItemsSource));
			});
		}

		#region ViewModel
		public DownloadsViewModel ViewModel
		{
			get { return (DownloadsViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(DownloadsViewModel), typeof(DownloadsView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (DownloadsViewModel)value; }
		}
		#endregion
	}
}
