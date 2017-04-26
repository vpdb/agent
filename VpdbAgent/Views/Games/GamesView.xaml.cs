using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
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
		public DataStatus DataFilter { get; set; }

		public GamesView()
		{
			InitializeComponent();

			//this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.DataContext);
			this.WhenActivated(d =>
			{
				d(this.OneWayBind(ViewModel, vm => vm.Systems, v => v.SystemList.ItemsSource));
				d(this.OneWayBind(ViewModel, vm => vm.Games, v => v.GameList.ItemsSource));
				d(this.BindCommand(ViewModel, vm => vm.IdentifyAll, v => v.IdentifyAllButton));
				d(this.Bind(ViewModel, vm => vm.ShowDisabled, v => v.ShowDisabled.IsChecked));
				d(this.Bind(ViewModel, vm => vm.ShowHidden, v => v.ShowHidden.IsChecked));

				new[] { FilterAll, FilterFilesNotInDatabase, FilterGamesNotOnDisk, FilterUnmappedFiles }
					.Select(y => y.WhenAny(x => x.IsChecked, x => x).Where(x => x.Value == true).Select(x => x.Sender.Tag))
					.Merge()
					.Subscribe(x => ViewModel.DataFilter = (DataStatus)Enum.Parse(typeof(DataStatus), (string)x));

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
