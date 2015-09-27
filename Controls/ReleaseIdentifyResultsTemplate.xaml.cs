using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Controls
{
	/// <summary>
	/// Interaction logic for ReleaseIdentifyResultsTemplate.xaml
	/// </summary>
	public partial class ReleaseIdentifyResultsTemplate : UserControl
	{
		private List<Release> _releases;
		private readonly GameTemplate.IReleaseResult _callback;

		public List<Release> Releases
		{
			get { return _releases; }
			set
			{
				_releases = value;
				UpdateElements();
			}
		}

		public ReleaseIdentifyResultsTemplate(List<Release> releases, GameTemplate.IReleaseResult callback)
		{
			if (releases == null) {
				throw new ArgumentNullException(nameof(releases));
			}
			if (callback == null) {
				throw new ArgumentNullException(nameof(callback));
			}
			_releases = releases;
			_callback = callback;
			InitializeComponent();
			DataContext = this;
			UpdateElements();
		}

		private void UpdateElements()
		{
			if (_releases.Count > 0) {
				NoResult.Visibility = Visibility.Collapsed;
				Results.Visibility = Visibility.Visible;

			} else {
				NoResult.Visibility = Visibility.Visible;
				Results.Visibility = Visibility.Collapsed;
			}
		}

		private void SelectButton_Click(object sender, RoutedEventArgs e)
		{
			var button = sender as Button;
			if (button != null) {
				_callback.OnResult(button.DataContext as Release);
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			_callback.OnCanceled();
		}


	}
}
