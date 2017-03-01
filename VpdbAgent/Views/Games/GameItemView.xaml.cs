using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Devart.Controls;
using ReactiveUI;
using VpdbAgent.ViewModels.Games;

namespace VpdbAgent.Views.Games
{
	/// <summary>
	/// Interaction logic for GameTemplate.xaml
	/// </summary>
	[ExcludeFromCodeCoverage]
	public partial class GameItemView : IViewFor<GameItemViewModel>, IHeightMeasurer
	{
		public GameItemView()
		{
			InitializeComponent();
		}

		public double GetEstimatedHeight(double availableWidth)
		{
			return 200;
		}

		#region ViewModel
		public GameItemViewModel ViewModel
		{
			get { return (GameItemViewModel)GetValue(ViewModelProperty); }
			set { SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(GameItemViewModel), typeof(GameItemView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (GameItemViewModel)value; }
		}
		#endregion

		public override string ToString()
		{
			return $"[GameView] {(DataContext as GameItemViewModel)?.Game}";
		}
	}
}
