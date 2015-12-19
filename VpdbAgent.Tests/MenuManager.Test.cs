using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Moq;
using NLog;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Tests.Mocks;
using Xunit;

namespace VpdbAgent.Tests
{
	public class MenuManager
	{
		[Fact]
		public void ShouldReadGames()
		{

			var resolver = new ModernDependencyResolver();
			var logger = LogManager.GetCurrentClassLogger();
			var fsw = new Mock<IFileSystemWatcher>();
			var fam = new Mock<IFileAccessManager>();
			var sm = new Mock<ISettingsManager>();
			sm.Setup(s => s.Settings).Returns(TestConfig.GenerateSettings());
			fam.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(TestConfig.GeneratePinballXIni());

			var fileWatcher = new Subject<string>();
			var databaseWatcher = new Subject<string>();
			fsw.Setup(f => f.FileWatcher(It.IsAny<string>())).Returns(fileWatcher);
			fsw.Setup(f => f.DatabaseWatcher(It.IsAny<string>(), It.IsAny<IList<PinballXSystem>>())).Returns(databaseWatcher);

			var menuManager = new PinballX.MenuManager(
				fsw.Object,
				sm.Object, 
				fam.Object,
				new TestThreadManager(), 
				new Mock<CrashManager>(logger).Object,
				logger);

			menuManager.Initialize();

		}
	}

}