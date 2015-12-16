using Moq;
using NLog;
using VpdbAgent.Application;
using VpdbAgent.PinballX;
using Xunit;

namespace VpdbAgent.Tests
{
	public class MenuManager
	{
		[Fact]
		public void ShouldReadGames()
		{
			var logger = LogManager.GetCurrentClassLogger();
			var fam = new Mock<IFileAccessManager>();
			var sm = new Mock<ISettingsManager>();
			sm.Setup(s => s.Settings).Returns((new Mock<Settings>()).SetupAllProperties().Object);

			var menuManager = new PinballX.MenuManager(
				Mock.Of<IFileSystemWatcher>(),
				sm.Object, 
				fam.Object,
				new Mock<CrashManager>(logger).Object,
				logger);

			menuManager.Initialize();
		}
	}
}