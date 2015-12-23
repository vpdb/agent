using System;
using System.Diagnostics.CodeAnalysis;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.Common.TypeConverters.ReactiveUI;
using VpdbAgent.PinballX;
using VpdbAgent.ViewModels;
using VpdbAgent.ViewModels.Downloads;
using VpdbAgent.ViewModels.Games;
using VpdbAgent.ViewModels.Messages;
using VpdbAgent.ViewModels.Settings;
using VpdbAgent.Views;
using VpdbAgent.Views.Downloads;
using VpdbAgent.Views.Games;
using VpdbAgent.Views.Messages;
using VpdbAgent.Views.Settings;
using VpdbAgent.VisualPinball;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Download;

namespace VpdbAgent
{

	/* The Bootstrapper is a ViewModel for the WPF Application class.
     * Since Application isn't very testable (just like Window / UserControl), 
     * we want to create a class we can test. Since our application only has
     * one "screen" (i.e. a place we present Routed Views), we can also use 
     * this as our IScreen.
     * 
     * An IScreen is a ViewModel that contains a Router - practically speaking,
     * it usually represents a Window (or the RootFrame of a WinRT app). We 
     * should technically create a MainWindowViewModel to represent the IScreen,
     * but there isn't much benefit to split those up unless you've got multiple
     * windows.
     * 
     * Bootstrapper is a good place to implement a lot of the "global 
     * variable" type things in your application. It's also the place where
     * you should configure your IoC container. And finally, it's the place 
     * which decides which View to Navigate to when the application starts.
     */
	[ExcludeFromCodeCoverage]
	public class Bootstrapper : ReactiveObject, IScreen
	{
		public RoutingState Router { get; }

		public Bootstrapper(IMutableDependencyResolver deps = null, RoutingState testRouter = null)
		{
			Router = testRouter ?? new RoutingState();
			deps = deps ?? Locator.CurrentMutable;
			var options = ((App) System.Windows.Application.Current).CommandLineOptions;

			// Bind 
			RegisterParts(deps);

			// This is a good place to set up any other app 
			// startup tasks, like setting the logging level
			LogHost.Default.Level = LogLevel.Debug;
			var settingsManager = deps.GetService<ISettingsManager>();
			var gameManager = deps.GetService<IGameManager>();

			deps.GetService<NLog.ILogger>().Info("Waiting for settings...");

			settingsManager.ApiAuthenticated.Subscribe(user => {
				if (user != null) {
					((App)System.Windows.Application.Current).CrashManager.SetUser(user);
				}
			});

			// Navigate to the opening page of the application
			settingsManager.SettingsAvailable.Subscribe(settings => {
				if (settings == null) {
					return;
				}
				System.Windows.Application.Current.Dispatcher.Invoke(delegate {

					deps.GetService<NLog.ILogger>().Info("Got settings!");
					if (settings.IsFirstRun || string.IsNullOrEmpty(settings.ApiKey)) {
						System.Windows.Application.Current.MainWindow = new MainWindow(this);
						System.Windows.Application.Current.MainWindow.Show();
						Router.Navigate.Execute(new SettingsViewModel(this,
							deps.GetService<ISettingsManager>(),
							deps.GetService<IVersionManager>(),
							deps.GetService<IGameManager>())
						);

					} else if (!options.Minimized) {
						// start the initialization
						gameManager.Initialize();

						System.Windows.Application.Current.MainWindow = new MainWindow(this);
						System.Windows.Application.Current.MainWindow.Show();
						Router.Navigate.Execute(new MainViewModel(this, deps.GetService<ISettingsManager>(), deps.GetService<IVersionManager>()));
					} else {
						// start the initialization
						gameManager.Initialize();
					}
				});
			});
		}

		private void RegisterParts(IMutableDependencyResolver deps)
		{
			deps.RegisterConstant(this, typeof(IScreen));

			// services
			deps.RegisterLazySingleton(NLog.LogManager.GetCurrentClassLogger, typeof(NLog.ILogger));
			deps.RegisterLazySingleton(() => ((App)System.Windows.Application.Current).CrashManager, typeof(CrashManager));
			deps.RegisterLazySingleton(() => new ThreadManager(), typeof(IThreadManager));
			deps.RegisterLazySingleton(() => new Directory(), typeof(IDirectory));
			deps.RegisterLazySingleton(() => new File(), typeof(IFile));

			deps.RegisterLazySingleton(() => new SettingsManager(
				deps.GetService<NLog.ILogger>()
			), typeof(ISettingsManager));

			deps.RegisterLazySingleton(() => new MarshallManager(
				deps.GetService<NLog.ILogger>(),
				deps.GetService<CrashManager>()
			), typeof(IMarshallManager));

			deps.RegisterLazySingleton(() => new FileSystemWatcher(
				deps.GetService<NLog.ILogger>()
			), typeof(IFileSystemWatcher));

			deps.RegisterLazySingleton(() => new VersionManager(
				deps.GetService<CrashManager>(),
				deps.GetService<NLog.ILogger>()
			), typeof(IVersionManager));

			deps.RegisterLazySingleton(() => new VisualPinballManager(
				deps.GetService<CrashManager>(),
				deps.GetService<NLog.ILogger>()
			), typeof(IVisualPinballManager));

			deps.RegisterLazySingleton(() => new DatabaseManager(
				deps.GetService<ISettingsManager>(),
				deps.GetService<CrashManager>(),
				deps.GetService<NLog.ILogger>()
			), typeof(IDatabaseManager));

			deps.RegisterLazySingleton(() => new MessageManager(
				deps.GetService<IDatabaseManager>(),
				deps.GetService<CrashManager>()
			), typeof(IMessageManager));

			deps.RegisterLazySingleton(() => new MenuManager(
				deps.GetService<IFileSystemWatcher>(),
				deps.GetService<ISettingsManager>(),
				deps.GetService<IMarshallManager>(),
				deps.GetService<IThreadManager>(),
				deps.GetService<IFile>(),
				deps.GetService<IDirectory>(),
				deps.GetService<NLog.ILogger>()
			), typeof(IMenuManager));

			deps.RegisterLazySingleton(() => new PlatformManager(
				deps.GetService<IMenuManager>(),
				deps.GetService<IThreadManager>(),
				deps.GetService<NLog.ILogger>(),
				deps
			), typeof(IPlatformManager));

			deps.RegisterLazySingleton(() => new VpdbClient(
				deps.GetService<ISettingsManager>(),
				deps.GetService<IVersionManager>(),
				deps.GetService<IMessageManager>(),
				this,
				deps.GetService<NLog.ILogger>(),
				deps.GetService<CrashManager>()
			), typeof(IVpdbClient));

			deps.RegisterLazySingleton(() => new RealtimeManager(
				deps.GetService<IVpdbClient>(),
				deps.GetService<NLog.ILogger>()
			), typeof(IRealtimeManager));

			deps.RegisterLazySingleton(() => new JobManager(
				deps.GetService<IDatabaseManager>(),
				deps.GetService<IMessageManager>(),
				deps.GetService<NLog.ILogger>(),
				deps.GetService<CrashManager>()
			), typeof(IJobManager));

			deps.RegisterLazySingleton(() => new DownloadManager(
				deps.GetService<IPlatformManager>(),
				deps.GetService<IJobManager>(),
				deps.GetService<IVpdbClient>(),
				deps.GetService<ISettingsManager>(),
				deps.GetService<IMessageManager>(),
				deps.GetService<IDatabaseManager>(),
				deps.GetService<NLog.ILogger>(),
				deps.GetService<CrashManager>()
			), typeof(IDownloadManager));

			deps.RegisterLazySingleton(() => new GameManager(
				deps.GetService<IMenuManager>(),
				deps.GetService<IVpdbClient>(),
				deps.GetService<ISettingsManager>(),
				deps.GetService<IDownloadManager>(),
				deps.GetService<IDatabaseManager>(),
				deps.GetService<IVersionManager>(),
				deps.GetService<IPlatformManager>(),
				deps.GetService<IMessageManager>(),
				deps.GetService<IRealtimeManager>(),
				deps.GetService<IVisualPinballManager>(),
				deps.GetService<IThreadManager>(),
				deps.GetService<NLog.ILogger>()
			), typeof(IGameManager));

			// converters
			deps.RegisterConstant(new ImageToUrlTypeConverter(), typeof(IBindingTypeConverter));
			deps.RegisterConstant(new NullToCollapsedConverter(), typeof(IBindingTypeConverter));
			deps.RegisterConstant(new NullToFalseConverter(), typeof(IBindingTypeConverter));
			deps.RegisterConstant(new DictionaryToStringConverter(), typeof(IBindingTypeConverter));
			deps.RegisterConstant(new DictionaryToBooleanConverter(), typeof(IBindingTypeConverter));
			deps.RegisterConstant(new DictionaryToVisibilityConverter(), typeof(IBindingTypeConverter));
			deps.RegisterConstant(new BooleanToVisibilityConverter(), typeof(IBindingTypeConverter));

			// view models
			deps.RegisterLazySingleton(() => new MainView(), typeof(IViewFor<MainViewModel>));
			deps.RegisterLazySingleton(() => new GamesView(), typeof(IViewFor<GamesViewModel>));
			deps.RegisterLazySingleton(() => new DownloadsView(), typeof(IViewFor<DownloadsViewModel>));
			deps.RegisterLazySingleton(() => new MessagesView(), typeof(IViewFor<MessagesViewModel>));
			deps.Register(() => new GameItemView(), typeof(IViewFor<GameItemViewModel>));
			deps.Register(() => new GameResultItemView(), typeof(IViewFor<GameResultItemViewModel>));
			deps.Register(() => new DownloadItemView(), typeof(IViewFor<DownloadItemViewModel>));
			deps.Register(() => new MessageItemView(), typeof(IViewFor<MessageItemViewModel>));
			deps.RegisterLazySingleton(() => new SettingsView(), typeof(IViewFor<SettingsViewModel>));
		}
	}
}