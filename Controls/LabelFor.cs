using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace VpdbAgent.Controls
{
	public class LabelFor : Label
	{
		public override void BeginInit()
		{
			base.BeginInit();
			this.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
		}
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			if (Target != null) {
				Target.Focus();
				if (Target.GetType().Name == "TextBox") {
					var txt = (TextBox)Target;
					txt.CaretIndex = txt.Text.Length;
				}

				if (Target.GetType().Name == "ToggleButton") {
					var btn = (ToggleButton)Target;
					btn.IsChecked = !btn.IsChecked;
				}
			}
		}
	}
}
