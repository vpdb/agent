using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Devart.Controls;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.Common;
using VpdbAgent.Controls;
using VpdbAgent.ViewModels;
using VpdbAgent.ViewModels.TypeConverters;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.Views
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
