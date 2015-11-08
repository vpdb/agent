using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NLog;
using Squirrel;

namespace VpdbAgent
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : System.Windows.Application
	{
		private readonly Logger _logger = LogManager.GetCurrentClassLogger();

		public App()
		{
			_logger.Info("Starting application.");
		}
	}
}
