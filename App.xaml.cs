using System.Windows;
using ReactiveUI;
using Splat;
using VpdbAgent.ViewModels;
using VpdbAgent.Views;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			Locator.CurrentMutable.Register(() => new MainView(), typeof(IViewFor<MainViewModel>));
		}
	}
}
