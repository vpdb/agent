using System.Windows;
using NLog;
using ReactiveUI;
using Splat;
using VpdbAgent.ViewModels;
using VpdbAgent.ViewModels.Games;
using VpdbAgent.Views;
using VpdbAgent.Views.Games;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : System.Windows.Application
	{
		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		public App()
		{
			_logger.Info("Starting application.");
			Locator.CurrentMutable.Register(() => new GamesView(), typeof(IViewFor<GamesViewModel>));
		}
	}
}
