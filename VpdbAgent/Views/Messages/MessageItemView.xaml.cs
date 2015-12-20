using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using Devart.Controls;
using ReactiveUI;
using VpdbAgent.ViewModels.Messages;

namespace VpdbAgent.Views.Messages
{
	/// <summary>
	/// Interaction logic for MessageItemView.xaml
	/// </summary>
	[ExcludeFromCodeCoverage]
	public partial class MessageItemView : UserControl, IViewFor<MessageItemViewModel>, IHeightMeasurer
	{
		public MessageItemView()
		{
			InitializeComponent();
		}

		public double GetEstimatedHeight(double availableWidth)
		{
			return 200;
		}

		#region ViewModel
		public MessageItemViewModel ViewModel
		{
			get { return (MessageItemViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(MessageItemViewModel), typeof(MessageItemView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (MessageItemViewModel)value; }
		}
		#endregion

	}
}
