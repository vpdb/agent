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
using Splat;
using VpdbAgent.ViewModels;
using VpdbAgent.Views;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Views
{
	/// <summary>
	/// Interaction logic for ReleaseIdentifyResultsTemplate.xaml
	/// </summary>
	public partial class MainReleaseResultsView : UserControl, IViewFor<MainReleaseResultsViewModel>
	{
		
		public MainReleaseResultsView()
		{
			InitializeComponent();

			this.OneWayBind(ViewModel, vm => vm.IdentifiedReleases, v => v.Results.ItemsSource);
			this.OneWayBind(ViewModel, vm => vm.HasResults, v => v.Results.Visibility);
			this.OneWayBind(ViewModel, vm => vm.HasResults, v => v.NoResults.Visibility, null, BooleanToVisibilityHint.Inverse);
			this.OneWayBind(ViewModel, vm => vm.HasExecuted, v => v.Panel.IsExpanded);
		}

		/*
		
		private void SelectButton_Click(object sender, RoutedEventArgs e)
		{
			var button = sender as Button;
			if (button != null) {
				//_callback.OnResult(button.DataContext as Release);
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			//_callback.OnCanceled();
		}
		*/

		#region ViewModel
		public MainReleaseResultsViewModel ViewModel
		{
			get { return (MainReleaseResultsViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(MainReleaseResultsViewModel), typeof(MainReleaseResultsView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (MainReleaseResultsViewModel)value; }
		}
		#endregion
	}
}
