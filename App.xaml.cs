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
using SynchrotronNet;
using VpdbAgent.Application;
using VpdbAgent.ViewModels;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : System.Windows.Application
	{

		private readonly Logger _logger = LogManager.GetCurrentClassLogger();
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


			var original = new[] { "first line", "second line", "third line", "fourth line" };
			var originalChanged = new[] { "first line", "edited 2nd", "third line", "fourth line also" };
			var update = new[] { "first line", "second line", "third line", "fourth line has changed" };

			var result = Diff.Diff3Merge(originalChanged, original, update, true);

			foreach (var block in result) {
				var okBlock = block as Diff.MergeOkResultBlock;
				var conflictBlock = block as Diff.MergeConflictResultBlock;

				if (okBlock != null) {
					Console.WriteLine("------------------- Success: \n{0}", string.Join("\n", okBlock.ContentLines));

				} else if (conflictBlock != null) {
					Console.WriteLine("------------------- Conflict.");

				} else {
					throw new InvalidOperationException("Result must be either ok or conflict.");
				}
			}
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
