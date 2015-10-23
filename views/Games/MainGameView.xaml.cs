using System.Windows;
using System.Windows.Controls;
using Devart.Controls;
using ReactiveUI;
using VpdbAgent.ViewModels.Games;

namespace VpdbAgent.Views.Games
{
	/// <summary>
	/// Interaction logic for GameTemplate.xaml
	/// </summary>
	public partial class MainGameView : UserControl, IViewFor<MainGameViewModel>, IHeightMeasurer
	{
		public MainGameView()
		{
			InitializeComponent();
		}

		public double GetEstimatedHeight(double availableWidth)
		{
			return 200;
		}

		#region ViewModel
		public MainGameViewModel ViewModel
		{
			get { return (MainGameViewModel)this.GetValue(ViewModelProperty); }
			set { SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(MainGameViewModel), typeof(MainGameView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (MainGameViewModel)value; }
		}
		#endregion

		public override string ToString()
		{
			return $"[GameView] {(DataContext as MainGameViewModel)?.Game}";
		}
	}
}
