using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Common.TypeConverters;
using VpdbAgent.PinballX;
using VpdbAgent.ViewModels.Downloads;
using VpdbAgent.ViewModels.Games;
using VpdbAgent.ViewModels.Settings;
using VpdbAgent.Views;
using VpdbAgent.Views.Downloads;
using VpdbAgent.Views.Games;
using VpdbAgent.Vpdb;

namespace VpdbAgent.ViewModels
{

	/* The AppViewModel is a ViewModel for the WPF Application class.
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
     * AppViewModel is a good place to implement a lot of the "global 
     * variable" type things in your application. It's also the place where
     * you should configure your IoC container. And finally, it's the place 
     * which decides which View to Navigate to when the application starts.
     */
	public class AppViewModel : ReactiveObject, IScreen
	{
		public RoutingState Router { get; private set; }

		// commands
		public ReactiveCommand<object> GotoSettings { get; protected set; }

		public AppViewModel(IMutableDependencyResolver dependencyResolver = null, RoutingState testRouter = null)
		{
			Router = testRouter ?? new RoutingState();
			dependencyResolver = dependencyResolver ?? Locator.CurrentMutable;

			//var canGoBack = this.WhenAnyValue(vm => vm.Router.NavigationStack.Count).Select(count => count > 0);
			//BackCommand =  ReactiveCommand.Create(canGoBack);

			// Bind 
			RegisterParts(dependencyResolver);

			// This is a good place to set up any other app 
			// startup tasks, like setting the logging level
			LogHost.Default.Level = LogLevel.Debug;
			var settingsManager = Locator.Current.GetService<ISettingsManager>();

			// Navigate to the opening page of the application
			if (settingsManager.IsInitialized()) {
				Router.Navigate.Execute(new MainViewModel(this));
			} else {
				Router.Navigate.Execute(new SettingsViewModel(
					this, 
					Locator.Current.GetService<ISettingsManager>())
				);
			}

			GotoSettings = ReactiveCommand.CreateAsyncObservable(_ => Router.Navigate.ExecuteAsync(new SettingsViewModel(this, Locator.Current.GetService<ISettingsManager>())));
		}

		private void RegisterParts(IMutableDependencyResolver locator)
		{
			locator.RegisterConstant(this, typeof(IScreen));


			// services
			locator.RegisterLazySingleton(NLog.LogManager.GetCurrentClassLogger, typeof(NLog.Logger));
			locator.RegisterLazySingleton(() => new SettingsManager(), typeof(ISettingsManager));
			locator.RegisterLazySingleton(() => new FileSystemWatcher(
				locator.GetService<NLog.Logger>()
			), typeof(IFileSystemWatcher));

			locator.RegisterLazySingleton(() => new DatabaseManager(
				locator.GetService<ISettingsManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IDatabaseManager));

			locator.RegisterLazySingleton(() => new MenuManager(
				locator.GetService<IFileSystemWatcher>(),
				locator.GetService<ISettingsManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IMenuManager));

			locator.RegisterLazySingleton(() => new VpdbClient(
				locator.GetService<ISettingsManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IVpdbClient));

			locator.RegisterLazySingleton(() => new DownloadManager(
				locator.GetService<IVpdbClient>(),
				locator.GetService<IDatabaseManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IDownloadManager));

			locator.RegisterLazySingleton(() => new GameManager(
				locator.GetService<IMenuManager>(),
				locator.GetService<IVpdbClient>(),
				locator.GetService<ISettingsManager>(),
				locator.GetService<IDownloadManager>(),
				locator.GetService<IDatabaseManager>(),
				locator.GetService<NLog.Logger>()
			), typeof(IGameManager));

			// converters
			locator.RegisterConstant(new ImageToUrlTypeConverter(), typeof(IBindingTypeConverter));

			// view models
			locator.RegisterLazySingleton(() => new MainView(), typeof(IViewFor<MainViewModel>));
			locator.RegisterLazySingleton(() => new GamesView(), typeof(IViewFor<GamesViewModel>));
			locator.RegisterLazySingleton(() => new DownloadsView(), typeof(IViewFor<DownloadsViewModel>));
			locator.Register(() => new GameItemView(), typeof(IViewFor<GameItemViewModel>));
			locator.Register(() => new GameResultItemView(), typeof(IViewFor<GameResultItemViewModel>));
			locator.Register(() => new DownloadItemView(), typeof(IViewFor<DownloadItemViewModel>));
			locator.RegisterLazySingleton(() => new SettingsView(), typeof(IViewFor<SettingsViewModel>));
		}
	}
}