using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VpdbAgent.Controls
{

	/// <summary>
	/// InterminateProgressSpinner adapted from http://www.codeproject.com/Articles/49853/Better-WPF-Circular-Progress-Bar
	/// </summary>
	public class IndeterminateProgress : Control
	{
		private Ellipse _c0;
		private Ellipse _c1;
		private Ellipse _c2;
		private Ellipse _c3;
		private Ellipse _c4;
		private Ellipse _c5;
		private Ellipse _c6;
		private Ellipse _c7;
		private Ellipse _c8;
		private RotateTransform _spinnerRotate;
		
		private readonly DispatcherTimer _animationTimer = new DispatcherTimer(DispatcherPriority.ContextIdle);
		
		static IndeterminateProgress()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(IndeterminateProgress), new FrameworkPropertyMetadata(typeof(IndeterminateProgress)));
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			_c0 = GetTemplateChild("C0") as Ellipse;
			_c1 = GetTemplateChild("C1") as Ellipse;
			_c2 = GetTemplateChild("C2") as Ellipse;
			_c3 = GetTemplateChild("C3") as Ellipse;
			_c4 = GetTemplateChild("C4") as Ellipse;
			_c5 = GetTemplateChild("C5") as Ellipse;
			_c6 = GetTemplateChild("C6") as Ellipse;
			_c7 = GetTemplateChild("C7") as Ellipse;
			_c8 = GetTemplateChild("C8") as Ellipse;
			_spinnerRotate = GetTemplateChild("SpinnerRotate") as RotateTransform;
			_animationTimer.Interval = new TimeSpan(0, 0, 0, 0, 75);

			const double offset = Math.PI;
			const double step = Math.PI * 2 / 10.0;

			SetPosition(_c0, offset, 0.0, step);
			SetPosition(_c1, offset, 1.0, step);
			SetPosition(_c2, offset, 2.0, step);
			SetPosition(_c3, offset, 3.0, step);
			SetPosition(_c4, offset, 4.0, step);
			SetPosition(_c5, offset, 5.0, step);
			SetPosition(_c6, offset, 6.0, step);
			SetPosition(_c7, offset, 7.0, step);
			SetPosition(_c8, offset, 8.0, step);
		}

		private void SetPosition(Ellipse ellipse, double offset, double posOffSet, double step)
		{
			ellipse.SetValue(Canvas.LeftProperty, 50.0 + Math.Sin(offset + posOffSet * step) * 50.0); ellipse.SetValue(Canvas.TopProperty, 50 + Math.Cos(offset + posOffSet * step) * 50.0);
		}

		public void Start()
		{
			_animationTimer.Tick += HandleAnimationTick;
			_animationTimer.Start();
		}

		public void Stop()
		{
			_animationTimer.Stop();
			_animationTimer.Tick -= HandleAnimationTick;
		}

		private void HandleAnimationTick(object sender, EventArgs e)
		{
			_spinnerRotate.Angle = (_spinnerRotate.Angle + 12) % 360;
		}
	}
}

