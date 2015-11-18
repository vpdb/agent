using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Splat;
using VpdbAgent.Application;

namespace VpdbAgent.ViewModels
{
	/// <summary>
	/// Provides bindable properties and commands for the NotifyIcon. In this sample, the
	/// view model is assigned to the NotifyIcon in XAML. Alternatively, the startup routing
	/// in App.xaml.cs could have created this view model, and assigned it to the NotifyIcon.
	/// </summary>
	public class NotifyIconViewModel
	{
		/// <summary>
		/// Shows a window, if none is already open.
		/// </summary>
		public ICommand ShowWindowCommand
		{
			get
			{
				return new DelegateCommand {
					CanExecuteFunc = () => System.Windows.Application.Current.MainWindow == null,
					CommandAction = () => {
						var appViewModel = ((App) System.Windows.Application.Current).AppViewModel;
						System.Windows.Application.Current.MainWindow = new MainWindow(appViewModel);
						System.Windows.Application.Current.MainWindow.Show();
						appViewModel.Router.Navigate.Execute(new MainViewModel(appViewModel, Locator.Current.GetService<ISettingsManager>(), Locator.Current.GetService<IVersionManager>()));
					}
				};
			}
		}

		/// <summary>
		/// Hides the main window. This command is only enabled if a window is open.
		/// </summary>
		public ICommand HideWindowCommand
		{
			get
			{
				return new DelegateCommand {
					CommandAction = () => System.Windows.Application.Current.MainWindow.Close(),
					CanExecuteFunc = () => System.Windows.Application.Current.MainWindow != null
				};
			}
		}


		/// <summary>
		/// Shuts down the application.
		/// </summary>
		public ICommand ExitApplicationCommand
		{
			get
			{
				return new DelegateCommand { CommandAction = () => System.Windows.Application.Current.Shutdown() };
			}
		}
	}


	/// <summary>
	/// Simplistic delegate command for the demo.
	/// </summary>
	public class DelegateCommand : ICommand
	{
		public Action CommandAction { get; set; }
		public Func<bool> CanExecuteFunc { get; set; }

		public void Execute(object parameter)
		{
			CommandAction();
		}

		public bool CanExecute(object parameter)
		{
			return CanExecuteFunc == null || CanExecuteFunc();
		}

		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}
	}
}
