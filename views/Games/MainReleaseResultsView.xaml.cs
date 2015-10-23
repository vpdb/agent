using System.Windows;
using System.Windows.Controls;
using ReactiveUI;
using VpdbAgent.ViewModels.Games;

namespace VpdbAgent.Views.Games
{
	/// <summary>
	/// Interaction logic for ReleaseIdentifyResultsTemplate.xaml
	/// </summary>
	public partial class MainReleaseResultsView : UserControl, IViewFor<MainReleaseResultsViewModel>
	{
		public MainReleaseResultsView()
		{
			InitializeComponent();
			this.WhenActivated(d => {

				// results
				d(this.OneWayBind(ViewModel, vm => vm.IdentifiedReleases, v => v.Results.ItemsSource));

				// visibility
				d(this.OneWayBind(ViewModel, vm => vm.HasResults, v => v.Results.Visibility));
				d(this.OneWayBind(ViewModel, vm => vm.HasResults, v => v.NoResults.Visibility, null, BooleanToVisibilityHint.Inverse));
				d(this.OneWayBind(ViewModel, vm => vm.HasExecuted, v => v.Panel.IsExpanded));

				// commands
				d(this.BindCommand(ViewModel, vm => vm.CloseResults, v => v.CloseButton));
				d(this.BindCommand(ViewModel, vm => vm.CloseResults, v => v.ClosePanel));
			});
		}

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
