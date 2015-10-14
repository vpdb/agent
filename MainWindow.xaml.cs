using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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
			_currentOutput.Text = "Rendered in " + msec + "ms.";
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
