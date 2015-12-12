using System;
using Mindscape.Raygun4Net;
using Mindscape.Raygun4Net.Messages;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
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
	public class Bootstrapper : ReactiveObject, IScreen
	{
		public RoutingState Router { get; }

		public Bootstrapper(IMutableDependencyResolver dependencyResolver = null, RoutingState testRouter = null)
		{
			Router = testRouter ?? new RoutingState();
			dependencyResolver = dependencyResolver ?? Locator.CurrentMutable;
			var options = ((App) System.Windows.Application.Current).CommandLineOptions;

			// Bind 
			RegisterParts(dependencyResolver);

			// This is a good place to set up any other app 
			// startup tasks, like setting the logging level
			LogHost.Default.Level = LogLevel.Debug;
			var settingsManager = Locator.Current.GetService<ISettingsManager>();
			var gameManager = Locator.Current.GetService<IGameManager>();

			Locator.CurrentMutable.GetService<NLog.Logger>().Info("Waiting for settings...");

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

					Locator.CurrentMutable.GetService<NLog.Logger>().Info("Got settings!");
					if (settings.IsFirstRun || string.IsNullOrEmpty(settings.ApiKey)) {
						System.Windows.Application.Current.MainWindow = new MainWindow(this);
						System.Windows.Application.Current.MainWindow.Show();
						Router.Navigate.Execute(new SettingsViewModel(this, 
							Locator.Current.GetService<ISettingsManager>(),
							Locator.Current.GetService<IVersionManager>(),
							Locator.Current.GetService<IGameManager>())
						);

					} else if (!options.Minimized) {
						// start the initialization
						gameManager.Initialize();

						System.Windows.Application.Current.MainWindow = new MainWindow(this);
						System.Windows.Application.Current.MainWindow.Show();
						Router.Navigate.Execute(new MainViewModel(this, Locator.Current.GetService<ISettingsManager>(), Locator.Current.GetService<IVersionManager>()));
					} else {
						// start the initialization
						gameManager.Initialize();
					}
				});
			});
		}

		private void RegisterParts(IMutableDependencyResolver locator)
		{
			locator.RegisterConstant(this, typeof(IScreen));

			// services
			locator.RegisterLazySingleton(NLog.LogManager.GetCurrentClassLogger, typeof(NLog.Logger));
			locator.RegisterLazySingleton(() => ((App)System.Windows.Application.Current).CrashManager, typeof(CrashManager));

			locator.RegisterLazySingleton(() => new SettingsManager(
				locator.GetService<NLog.Logger>()
			), typeof(ISettingsManager));
			locator.RegisterLazySingleton(() => new FileSystemWatcher(
				locator.GetService<NLog.Logger>()
			), typeof(IFileSystemWatcher));
			locator.RegisterLazySingleton(() => new VersionManager(
				locator.GetService<CrashManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IVersionManager));
			locator.RegisterLazySingleton(() => new VisualPinballManager(
				locator.GetService<CrashManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IVisualPinballManager));

			locator.RegisterLazySingleton(() => new DatabaseManager(
				locator.GetService<ISettingsManager>(),
				locator.GetService<CrashManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IDatabaseManager));

			locator.RegisterLazySingleton(() => new MessageManager(
				locator.GetService<IDatabaseManager>(),
				locator.GetService<CrashManager>()
			), typeof(IMessageManager));

			locator.RegisterLazySingleton(() => new MenuManager(
				locator.GetService<IFileSystemWatcher>(),
				locator.GetService<ISettingsManager>(),
				locator.GetService<CrashManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IMenuManager));

			locator.RegisterLazySingleton(() => new PlatformManager(
				locator.GetService<IMenuManager>(),
				locator.GetService<IDatabaseManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IPlatformManager));

			locator.RegisterLazySingleton(() => new VpdbClient(
				locator.GetService<ISettingsManager>(),
				locator.GetService<IVersionManager>(),
				locator.GetService<IMessageManager>(),
				this,
				locator.GetService<CrashManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IVpdbClient));

			locator.RegisterLazySingleton(() => new RealtimeManager(
				locator.GetService<IVpdbClient>(),
				locator.GetService<NLog.Logger>()
			), typeof(IRealtimeManager));

			locator.RegisterLazySingleton(() => new JobManager(
				locator.GetService<IDatabaseManager>(),
				locator.GetService<IMessageManager>(),
				locator.GetService<CrashManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IJobManager));

			locator.RegisterLazySingleton(() => new DownloadManager(
				locator.GetService<IPlatformManager>(),
				locator.GetService<IJobManager>(),
				locator.GetService<IVpdbClient>(),
				locator.GetService<ISettingsManager>(),
				locator.GetService<IMessageManager>(),
				locator.GetService<IDatabaseManager>(),
				locator.GetService<CrashManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IDownloadManager));

			locator.RegisterLazySingleton(() => new GameManager(
				locator.GetService<IMenuManager>(),
				locator.GetService<IVpdbClient>(),
				locator.GetService<ISettingsManager>(),
				locator.GetService<IDownloadManager>(),
				locator.GetService<IDatabaseManager>(),
				locator.GetService<IVersionManager>(),
				locator.GetService<IPlatformManager>(),
				locator.GetService<IMessageManager>(),
				locator.GetService<IRealtimeManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IGameManager));

			// converters
			locator.RegisterConstant(new ImageToUrlTypeConverter(), typeof(IBindingTypeConverter));
			locator.RegisterConstant(new NullToCollapsedConverter(), typeof(IBindingTypeConverter));
			locator.RegisterConstant(new NullToFalseConverter(), typeof(IBindingTypeConverter));
			locator.RegisterConstant(new DictionaryToStringConverter(), typeof(IBindingTypeConverter));
			locator.RegisterConstant(new DictionaryToBooleanConverter(), typeof(IBindingTypeConverter));
			locator.RegisterConstant(new DictionaryToVisibilityConverter(), typeof(IBindingTypeConverter));
			locator.RegisterConstant(new BooleanToVisibilityConverter(), typeof(IBindingTypeConverter));

			// view models
			locator.RegisterLazySingleton(() => new MainView(), typeof(IViewFor<MainViewModel>));
			locator.RegisterLazySingleton(() => new GamesView(), typeof(IViewFor<GamesViewModel>));
			locator.RegisterLazySingleton(() => new DownloadsView(), typeof(IViewFor<DownloadsViewModel>));
			locator.RegisterLazySingleton(() => new MessagesView(), typeof(IViewFor<MessagesViewModel>));
			locator.Register(() => new GameItemView(), typeof(IViewFor<GameItemViewModel>));
			locator.Register(() => new GameResultItemView(), typeof(IViewFor<GameResultItemViewModel>));
			locator.Register(() => new DownloadItemView(), typeof(IViewFor<DownloadItemViewModel>));
			locator.Register(() => new MessageItemView(), typeof(IViewFor<MessageItemViewModel>));
			locator.RegisterLazySingleton(() => new SettingsView(), typeof(IViewFor<SettingsViewModel>));
		}
	}
}