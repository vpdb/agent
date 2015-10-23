using System.Windows;
using System.Windows.Controls;
using ReactiveUI;
using VpdbAgent.ViewModels.Games;

namespace VpdbAgent.Views.Games
{
	/// <summary>
	/// Interaction logic for ReleaseIdentifyResultsTemplate.xaml
	/// </summary>
	public partial class GameItemResultsView : UserControl, IViewFor<GameItemResultsViewModel>
	{
		public GameItemResultsView()
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
		public GameItemResultsViewModel ViewModel
		{
			get { return (GameItemResultsViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(GameItemResultsViewModel), typeof(GameItemResultsView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (GameItemResultsViewModel)value; }
		}
		#endregion
	}
}
