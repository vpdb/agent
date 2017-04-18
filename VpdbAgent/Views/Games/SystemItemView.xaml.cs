using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ReactiveUI;
using VpdbAgent.ViewModels.Games;

namespace VpdbAgent.Views.Games
{
	/// <summary>
	/// Interaction logic for SystemItemView.xaml
	/// </summary>
	public partial class SystemItemView :  IViewFor<SystemItemViewModel> {

		public SystemItemView()
		{
			InitializeComponent();
		}

		public void OnPlatformFilterChanged(object sender, object e)
		{
			var checkbox = (sender as CheckBox);
			if (checkbox == null) {
				return;
			}
			var platformName = checkbox.Tag as string;
			//ViewModel.OnPlatformFilterChanged(platformName, checkbox.IsChecked == true);
		}

		public void OnExecutableFilterChanged(object sender, object e)
		{
			var checkbox = (sender as CheckBox);
			if (checkbox == null) {
				return;
			}
			var fileName = checkbox.Tag as string;
			//ViewModel.OnExecutableFilterChanged(fileName, checkbox.IsChecked == true);
		}

		#region ViewModel
		public SystemItemViewModel ViewModel
		{
			get { return (SystemItemViewModel)GetValue(ViewModelProperty); }
			set { SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(SystemItemViewModel), typeof(SystemItemView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (SystemItemViewModel)value; }
		}
		#endregion
	}
}
