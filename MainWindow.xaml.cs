using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using NLog;
using PusherClient;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.ViewModels;
using VpdbAgent.Views;
using VpdbAgent.Vpdb;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		public AppViewModel AppViewModel { get; protected set; }

		private static readonly ISettingsManager SettingsManager = Locator.CurrentMutable.GetService<ISettingsManager>();

		private bool _minimizing = false;

		public MainWindow(AppViewModel appViewModel)
		{
			RestoreWindowPlacement();
			InitializeComponent();

			AppViewModel = appViewModel;
			DataContext = appViewModel;

			CompositionTarget.Rendering += OnRendering;

			StartMeasuring();
		}

		private void RestoreWindowPlacement()
		{
			SettingsManager.SettingsAvailable.Subscribe(settings => {
				Height = settings.WindowPosition.Height;
				Width = settings.WindowPosition.Width;
				double top, left;
				var screen = Screen.FromHandle(new WindowInteropHelper(this).Handle);
				if (settings.WindowPosition.Max) {
					WindowState = WindowState.Maximized;
				}
				if (settings.WindowPosition.Top > 0) {
					top = settings.WindowPosition.Top;
					left = settings.WindowPosition.Left;
				} else {
					top = (screen.Bounds.Height - Height) / 2;
					left = (screen.Bounds.Width - Width) / 2;
				}

				// don't clip
				Top = Math.Max(0, top);
				Left = Math.Max(0, left);
			});

			// add handlers
			Closing += Window_Closing;
			StateChanged += Window_StateChanged;
		}

		private void Window_StateChanged(object sender, EventArgs e)
		{
			if (WindowState == WindowState.Minimized && SettingsManager.Settings.MinimizeToTray) {
				_minimizing = true;
				Close();
			}
		}

		public void Window_Closing(object sender, CancelEventArgs e)
		{
			var settings = SettingsManager.Settings.Copy();
			settings.WindowPosition = new Settings.Position {
				Top = Top,
				Left = Left,
				Height = Height,
				Width = Width,
				Max = WindowState == WindowState.Maximized
			};
			SettingsManager.SaveInternal(settings).Subscribe(_ => {
				System.Windows.Application.Current.Dispatcher.Invoke(delegate {
					if (!_minimizing) {
						System.Windows.Application.Current.Shutdown();
					}
				});
			});
		}

		#region Performance Measurements

		private static readonly Stopwatch Timer = new Stopwatch();
		private static long _msec;
		private static TextBlock _currentOutput, _pendingOutput;

		private void StartMeasuring()
		{
			//Status.Text = "Loading...";
			GC.Collect();
			//_pendingOutput = Status;
			Timer.Restart();
		}

		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);
			MainWindow.OnPreparationsComplete();
		}

		public static void OnPreparationsComplete()
		{
			_currentOutput = _pendingOutput;
			_msec = Timer.ElapsedMilliseconds;
		}

		public static void OnRendering(object sender, EventArgs e)
		{
			if (_currentOutput == null) {
				return;
			}

			/*
			 * Sanity check: if less than 100 msec have elapsed between the end of the
			 * preparations stage and this event, then we got a spurious event before
			 * the render thread has actually started working, and we try again later.
			 */

			var msec = Timer.ElapsedMilliseconds;
			if (msec - _msec < 500) {
				return;
			}

			Timer.Stop();
			_currentOutput.Text = "Loaded in " + msec + "ms.";
			_currentOutput = _pendingOutput = null;
		}

		public static void OnRenderingComplete()
		{
			Timer.Stop();
			if (_currentOutput == null) {
				return;
			}
			_currentOutput.Text = Timer.ElapsedMilliseconds.ToString();
			_currentOutput = _pendingOutput = null;
		}

		#endregion
	}
}
