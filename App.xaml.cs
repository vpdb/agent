using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using CommandLine;
using Hardcodet.Wpf.TaskbarNotification;
using NLog;
using Squirrel;
using VpdbAgent.ViewModels;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : System.Windows.Application
	{

		private readonly Logger _logger = LogManager.GetCurrentClassLogger();
		private TaskbarIcon _notifyIcon;
		public AppViewModel AppViewModel { get; private set; }
		public Options CommandLineOptions { get; }


		public App()
		{
			_logger.Info("Starting application.");
			CommandLineOptions = new Options();
			Parser.Default.ParseArguments(Environment.GetCommandLineArgs(), CommandLineOptions);
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			_notifyIcon = (TaskbarIcon)FindResource("TaskbarIcon");
			AppViewModel = new AppViewModel();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			_notifyIcon.Dispose();
			base.OnExit(e);
		}

		public class Options
		{
			[Option(HelpText = "Starts VPDB Client head-less. You can open the UI any time through the tray icon.")]
			public bool Minimized { get; set; }
		}
	}
}
