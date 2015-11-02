using System.Windows;
using System.Windows.Controls;
using ReactiveUI;
using VpdbAgent.ViewModels.Games;

namespace VpdbAgent.Views.Games
{
	/// <summary>
	/// An item in the search result in the identify release panel
	/// </summary>
	public partial class GameResultItemView : UserControl, IViewFor<GameResultItemViewModel>
	{
		public GameResultItemView()
		{
			InitializeComponent();

			this.WhenActivated(d => {

				d(this.OneWayBind(ViewModel, vm => vm.Release.Game.DisplayName, v => v.GameName.Text));
				d(this.OneWayBind(ViewModel, vm => vm.Release.Name, v => v.ReleaseName.Text));
				d(this.OneWayBind(ViewModel, vm => vm.File.Reference.Name, v => v.ItemBlock.ToolTip));
				d(this.OneWayBind(ViewModel, vm => vm.Version.Name, v => v.VersionName.Text));
				d(this.OneWayBind(ViewModel, vm => vm.File.Flavor.Orientation, v => v.Orientation.Content));
				d(this.OneWayBind(ViewModel, vm => vm.File.Flavor.Lighting, v => v.Lighting.Content));
				d(this.OneWayBind(ViewModel, vm => vm.File.Thumb, v => v.Thumb.UrlSource));
				d(this.BindCommand(ViewModel, vm => vm.SelectResult, v => v.SelectButton));
			});
		}

		#region ViewModel
		public GameResultItemViewModel ViewModel
		{
			get { return (GameResultItemViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(GameResultItemViewModel), typeof(GameResultItemView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (GameResultItemViewModel)value; }
		}
		#endregion
	}
}
