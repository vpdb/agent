using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using NLog;
using ReactiveUI;
using VpdbAgent.ViewModels.Games;

namespace VpdbAgent.Views.Games
{
	/// <summary>
	/// Interaction logic for MainPage.xaml
	/// </summary>
	[ExcludeFromCodeCoverage]
	public partial class GamesView : UserControl, IViewFor<GamesViewModel>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public GamesView()
		{
			InitializeComponent();

			//this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.DataContext);
			this.WhenActivated(d =>
			{
				d(this.OneWayBind(ViewModel, vm => vm.Systems, v => v.SystemList.ItemsSource));
				d(this.OneWayBind(ViewModel, vm => vm.Games, v => v.GameList.ItemsSource));
				d(this.BindCommand(ViewModel, vm => vm.IdentifyAll, v => v.IdentifyAllButton));
			});
		}

		#region ViewModel
		public GamesViewModel ViewModel
		{
			get { return (GamesViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(GamesViewModel), typeof(GamesView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (GamesViewModel)value; }
		}
		#endregion
	}
}
