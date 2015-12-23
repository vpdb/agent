using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommandLine;
using Hardcodet.Wpf.TaskbarNotification;
using Mindscape.Raygun4Net;
using NLog;
using Squirrel;
using SynchrotronNet;
using VpdbAgent.Application;
using VpdbAgent.ViewModels;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	[ExcludeFromCodeCoverage]
	public partial class App : System.Windows.Application
	{

		private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
		public readonly CrashManager CrashManager;

		private TaskbarIcon _notifyIcon;
		public Bootstrapper Bootstrapper { get; private set; }
		public Options CommandLineOptions { get; }


		public App()
		{
			_logger.Info("Starting application.");
			CommandLineOptions = new Options();
			Parser.Default.ParseArguments(Environment.GetCommandLineArgs(), CommandLineOptions);

			// crash handling
			CrashManager = new CrashManager(_logger);
			DispatcherUnhandledException += CrashManager.OnDispatcherUnhandledException;
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

	}
}
