using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using ReactiveUI;

namespace VpdbAgent.Views
{
	public abstract class BaseView : UserControl, IReactiveView
	{

		public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
			"ViewModel",
			typeof(object),
			typeof(BaseView),
			new PropertyMetadata(null)
		);

		object IViewFor.ViewModel
		{
			get { return GetValue(ViewModelProperty); }
			set { SetValue(ViewModelProperty, value); }
		}
	}

	public interface IReactiveView : IViewFor
	{
	}

	public interface IReactiveView<T> : IReactiveView, IViewFor<T> where T : class
	{
	}
}
