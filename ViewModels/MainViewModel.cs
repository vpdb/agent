using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.ViewModels.Downloads;
using VpdbAgent.ViewModels.Games;
using VpdbAgent.ViewModels.Settings;
using VpdbAgent.Vpdb;

namespace VpdbAgent.ViewModels
{
	public class MainViewModel : ReactiveObject, IRoutableViewModel
	{
		private static readonly Version AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
		// commands
		public ReactiveCommand<object> GotoSettings { get; protected set; }

		// screen
		public IScreen HostScreen { get; protected set; }
		public string UrlPathSegment => "main";

		private readonly ObservableAsPropertyHelper<string> _loginStatus;
		public string LoginStatus => _loginStatus.Value;
		public string VersionName => $"VPDB Agent v{AppVersion.Major}.{AppVersion.Minor}.{AppVersion.Build}";

		// tabs
		public GamesViewModel Games { get; }
		public DownloadsViewModel Downloads { get; }

		public MainViewModel(IScreen screen, ISettingsManager settingsManager)
		{
			HostScreen = screen;

			Games = new GamesViewModel(Locator.Current.GetService<IGameManager>());
			Downloads = new DownloadsViewModel();
			GotoSettings = ReactiveCommand.CreateAsyncObservable(_ => screen.Router.Navigate.ExecuteAsync(new SettingsViewModel(screen, Locator.Current.GetService<ISettingsManager>())));

			settingsManager.WhenAnyValue(sm => sm.AuthenticatedUser)
				.Select(u => u == null ? "Not logged." : $"Logged as {u.Name}")
				.ToProperty(this, x => x.LoginStatus, out _loginStatus);

		}
	}
}
