using System.Windows;
using PusherClient;
using VpdbAgent.ViewModels;
using VpdbAgent.Views;
using VpdbAgent.Vpdb;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{

		public AppViewModel AppViewModel { get; protected set; }

		public MainWindow()
		{
			InitializeComponent();

			AppViewModel = new AppViewModel();
			DataContext = AppViewModel;
		}
	}
}
