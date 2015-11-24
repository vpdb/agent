using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommandLine;
using Hardcodet.Wpf.TaskbarNotification;
using Mindscape.Raygun4Net;
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
		public Bootstrapper Bootstrapper { get; private set; }
		public Options CommandLineOptions { get; }

		public readonly RaygunClient Raygun = new RaygunClient("rDGC5mT6YBc77sU8bm5/Jw==");

		public App()
		{
			_logger.Info("Starting application.");
			CommandLineOptions = new Options();
			Parser.Default.ParseArguments(Environment.GetCommandLineArgs(), CommandLineOptions);

			// crash handling
			DispatcherUnhandledException += OnDispatcherUnhandledException;
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			_notifyIcon = (TaskbarIcon)FindResource("TaskbarIcon");
			Bootstrapper = new Bootstrapper();
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

		void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			_logger.Error(e.Exception, "Uncatched error!");
#if !DEBUG
			Raygun.Send(e.Exception);
#endif
		}
	}
}
