using NLog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using VpdbAgent.Vpdb;
using PusherClient;
using System;
using System.Windows;
using System.Collections.Generic;
using ReactiveUI;
using VpdbAgent.ViewModels;
using Splat;
using System.Reactive.Linq;

namespace VpdbAgent.Views
{
	/// <summary>
	/// Interaction logic for MainPage.xaml
	/// </summary>
	public partial class MainView : UserControl, IViewFor<MainViewModel>
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public MainView()
		{
			InitializeComponent();

			//this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.DataContext);
			this.WhenActivated(d =>
			{
				d(this.OneWayBind(ViewModel, vm => vm.Platforms, v => v.PlatformList.ItemsSource));
				d(this.OneWayBind(ViewModel, vm => vm.Games, v => v.GameList.ItemsSource));
			});
		}

		public void OnPlatformFilterChanged(object sender, object e)
		{
			ViewModel.OnPlatformFilterChanged(sender, e);
		}

		#region ViewModel
		public MainViewModel ViewModel
		{
			get { return (MainViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(MainViewModel), typeof(MainView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (MainViewModel)value; }
		}
		#endregion
	}
}
