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
		public List<Release> Releases { get; private set; }

		public ReleaseIdentifyResultsTemplate(List<Release> releases)
		{
			Releases = releases;
			InitializeComponent();
			DataContext = this;

			if (releases.Count > 0) {
				NoResult.Visibility = Visibility.Collapsed;
				Results.Visibility = Visibility.Visible;

			} else {
				NoResult.Visibility = Visibility.Visible;
				Results.Visibility = Visibility.Collapsed;
			}
		}
	}
}
