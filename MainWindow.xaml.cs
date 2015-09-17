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
using MahApps.Metro.Controls;
using Refit;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		public MainWindow()
		{
			InitializeComponent();
			GetReleases();
		}

		private async void GetReleases()
		{
			VpdbApi vpdbApi = RestService.For<VpdbApi>("http://localhost:3000");

			List<Release> releases = await vpdbApi.GetReleases();
			foreach (Release release in releases)
			{
				Console.WriteLine("{0} ({1})", release.Name, release.Id);
			}
		}
	}
}
