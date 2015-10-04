using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Splat;
using VpdbAgent.Views;

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
     * AppBootstrapper is a good place to implement a lot of the "global 
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

			// Bind 
			RegisterParts(dependencyResolver);

			// This is a good place to set up any other app 
			// startup tasks, like setting the logging level
			LogHost.Default.Level = LogLevel.Debug;
			var settingsManager = SettingsManager.GetInstance();

			// Navigate to the opening page of the application
			if (settingsManager.IsInitialized()) {
				Router.Navigate.Execute(new MainViewModel(this));
			} else {
				Router.Navigate.Execute(new SettingsViewModel(this));
			}

			GotoSettings = ReactiveCommand.CreateAsyncObservable(_ => Router.Navigate.ExecuteAsync(new SettingsViewModel(this)));
		}

		private void RegisterParts(IMutableDependencyResolver dependencyResolver)
		{
			dependencyResolver.RegisterConstant(this, typeof(IScreen));
			dependencyResolver.Register(() => new MainView(), typeof(IViewFor<MainViewModel>));
			dependencyResolver.Register(() => new SettingsView(), typeof(IViewFor<SettingsViewModel>));
		}
	}
}