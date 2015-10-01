using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using ReactiveUI;

namespace VpdbAgent.Pages
{
	public abstract class BasePage : Page, IReactiveView
	{
		public BasePage() { }

		public static readonly DependencyProperty ViewModelProperty =
			   DependencyProperty.Register("ViewModel", typeof(object), typeof(BasePage), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return GetValue(ViewModelProperty); }
			set { SetValue(ViewModelProperty, value); }
		}
	}
}
