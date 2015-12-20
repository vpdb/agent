using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace VpdbAgent.Controls
{
	[ExcludeFromCodeCoverage]
	public class RichTextBlock : TextBlock
	{
		public ObservableCollection<Inline> InlineList
		{
			get { return (ObservableCollection<Inline>)GetValue(InlineListProperty); }
			set { SetValue(InlineListProperty, value); }
		}

		public static readonly DependencyProperty InlineListProperty =
		   DependencyProperty.Register("InlineList", typeof(ObservableCollection<Inline>), typeof(RichTextBlock), new UIPropertyMetadata(null, OnPropertyChanged));

		private static void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender != null && e.NewValue != null) {
				var textBlock = (RichTextBlock)sender;
				textBlock.Inlines.Clear();
				textBlock.Inlines.AddRange((ObservableCollection<Inline>)e.NewValue);
			}
		}
	}
}
