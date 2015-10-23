using System.Windows;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.ViewModels;
using VpdbAgent.ViewModels.Games;
using VpdbAgent.Views;
using MainView = VpdbAgent.Views.Games.MainView;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		public App()
		{
			_logger.Info("Starting application.");
			Locator.CurrentMutable.Register(() => new MainView(), typeof(IViewFor<MainViewModel>));
		}
	}
}
