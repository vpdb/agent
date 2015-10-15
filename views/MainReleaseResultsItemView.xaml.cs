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
	/// An item in the search result in the identify release panel
	/// </summary>
	public partial class MainReleaseResultsItemView : UserControl, IViewFor<MainReleaseResultsItemViewModel>
	{
		public MainReleaseResultsItemView()
		{
			InitializeComponent();

			this.WhenActivated(d => {

				d(this.OneWayBind(ViewModel, vm => vm.Release.Game.DisplayName, v => v.GameName.Text));
				d(this.OneWayBind(ViewModel, vm => vm.Release.Name, v => v.ReleaseName.Text));
				d(this.OneWayBind(ViewModel, vm => vm.Release.LatestVersion.Thumb.Image, v => v.Thumb.UrlSource));
				d(this.BindCommand(ViewModel, vm => vm.SelectResult, v => v.SelectButton));
			});
		}

		#region ViewModel
		public MainReleaseResultsItemViewModel ViewModel
		{
			get { return (MainReleaseResultsItemViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(MainReleaseResultsItemViewModel), typeof(MainReleaseResultsItemView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (MainReleaseResultsItemViewModel)value; }
		}
		#endregion
	}
}
