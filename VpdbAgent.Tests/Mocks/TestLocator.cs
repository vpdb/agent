using System.Collections.Generic;
using System.Reactive.Subjects;
using Moq;
using NLog;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Tests.Mocks
{
	public class TestLocator
	{

		public static IDependencyResolver Generate()
		{
			var resolver = new ModernDependencyResolver();

			// Logger
			resolver.RegisterLazySingleton(LogManager.GetCurrentClassLogger, typeof(Logger));

			// IFileSystemWatcher
			var fsw = new Mock<IFileSystemWatcher>();
			var fileWatcher = new Subject<string>();
			var databaseWatcher = new Subject<string>();
			fsw.Setup(f => f.FileWatcher(It.IsAny<string>())).Returns(fileWatcher);
			fsw.Setup(f => f.DatabaseWatcher(It.IsAny<string>(), It.IsAny<IList<PinballXSystem>>())).Returns(databaseWatcher);
			resolver.RegisterLazySingleton(() => fsw.Object, typeof(IFileSystemWatcher));

			// ISettingsManager
			var sm = new Mock<ISettingsManager>();
			sm.Setup(s => s.Settings).Returns(TestConfig.GenerateSettings());
			resolver.RegisterLazySingleton(() => sm.Object, typeof(ISettingsManager));

			// CrashManager
			resolver.RegisterLazySingleton(() => new Mock<CrashManager>(LogManager.GetCurrentClassLogger()).Object, typeof(CrashManager));

			// IThreadManager
			resolver.RegisterLazySingleton(() => new TestThreadManager(), typeof(IThreadManager));

			return resolver;
		}
	}
}
