using System.Windows;
using System.Windows.Controls;
using ReactiveUI;
using VpdbAgent.ViewModels.Games;

namespace VpdbAgent.Views.Games
{
	/// <summary>
	/// An item in the search result in the identify release panel
	/// </summary>
	public partial class GameItemResultsItemView : UserControl, IViewFor<GameItemResultsItemViewModel>
	{
		public GameItemResultsItemView()
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
		public GameItemResultsItemViewModel ViewModel
		{
			get { return (GameItemResultsItemViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(GameItemResultsItemViewModel), typeof(GameItemResultsItemView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (GameItemResultsItemViewModel)value; }
		}
		#endregion
	}
}
