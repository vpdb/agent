using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using PusherClient;
using ReactiveUI;
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

		public MainWindow()
		{
			RestoreWindowPlacement();
			InitializeComponent();

			AppViewModel = new AppViewModel();
			DataContext = AppViewModel;

			CompositionTarget.Rendering += OnRendering;

			StartMeasuring();
		}

		private void SettingsButton_Click(object sender, RoutedEventArgs e)
		{
			AppViewModel.GotoSettings.Execute(null);
		}

		private void RestoreWindowPlacement()
		{
			Height = Properties.Settings.Default.WindowHeight;
			Width = Properties.Settings.Default.WindowWidth;
			double top, left;
			var screen = Screen.FromHandle(new WindowInteropHelper(this).Handle);
			if (Properties.Settings.Default.WindowMax) {
				WindowState = WindowState.Maximized;
			}
			if (Properties.Settings.Default.WindowTop > 0) {
				top = Properties.Settings.Default.WindowTop;
				left = Properties.Settings.Default.WindowLeft;
			} else {
				top = (screen.Bounds.Height - Height) / 2;
				left = (screen.Bounds.Width - Width) / 2;
			}

			// don't clip
			Top = Math.Max(0, top);
			Left = Math.Max(0, left);

			// add save handler
			Closing += Window_Closing;
		}

		public void Window_Closing(object sender, CancelEventArgs e)
		{
			Properties.Settings.Default.WindowTop = Top;
			Properties.Settings.Default.WindowLeft = Left;
			Properties.Settings.Default.WindowHeight = Height;
			Properties.Settings.Default.WindowWidth = Width;
			Properties.Settings.Default.WindowMax = WindowState == WindowState.Maximized;
			Properties.Settings.Default.Save();
		}


		#region Performance Measurements

		private static readonly Stopwatch Timer = new Stopwatch();
		private static long _msec;
		private static TextBlock _currentOutput, _pendingOutput;

		private void StartMeasuring()
		{
			Status.Text = "Loading...";
			GC.Collect();
			_pendingOutput = Status;
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
