using System.Windows;
using System.Windows.Controls;
using ReactiveUI;
using VpdbAgent.ViewModels.Downloads;
using VpdbAgent.ViewModels.Messages;

namespace VpdbAgent.Views.Messages
{
	/// <summary>
	/// Interaction logic for MessagesView.xaml
	/// </summary>
	public partial class MessagesView : UserControl, IViewFor<MessagesViewModel>
	{
		public MessagesView()
		{
			InitializeComponent();

			this.WhenActivated(d => {
				d(this.OneWayBind(ViewModel, vm => vm.Messages, v => v.DownloadList.ItemsSource));
				d(this.OneWayBind(ViewModel, vm => vm.IsEmpty, v => v.EmptyLabel.Visibility));
			});
		}

		#region ViewModel
		public MessagesViewModel ViewModel
		{
			get { return (MessagesViewModel)this.GetValue(ViewModelProperty); }
			set { this.SetValue(ViewModelProperty, value); }
		}

		public static readonly DependencyProperty ViewModelProperty =
		   DependencyProperty.Register("ViewModel", typeof(MessagesViewModel), typeof(MessagesView), new PropertyMetadata(null));

		object IViewFor.ViewModel
		{
			get { return ViewModel; }
			set { ViewModel = (MessagesViewModel)value; }
		}
		#endregion
	}
}
