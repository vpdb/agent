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
	public class TestDependencies
	{
		public IDependencyResolver Locator => _locator;
		public Subject<string> PinballXIniWatcher { get; } = new Subject<string>();
		public Subject<string> DatabaseWatcher { get; } = new Subject<string>();
		public Subject<string> TableWatcher { get; } = new Subject<string>();

		public Mock<IFileSystemWatcher> FileSystemWatcher;

		private readonly ModernDependencyResolver _locator;

		public TestDependencies()
		{
			_locator = new ModernDependencyResolver();

			// Logger
			_locator.RegisterLazySingleton(LogManager.GetCurrentClassLogger, typeof(Logger));

			// IFileSystemWatcher
			FileSystemWatcher = new Mock<IFileSystemWatcher>();
			FileSystemWatcher.Setup(f => f.FileWatcher(It.IsAny<string>())).Returns(PinballXIniWatcher);
			FileSystemWatcher.Setup(f => f.DatabaseWatcher(It.IsAny<string>(), It.IsAny<IList<PinballXSystem>>())).Returns(DatabaseWatcher);
			FileSystemWatcher.Setup(f => f.TablesWatcher(It.IsAny<IList<PinballXSystem>>())).Returns(TableWatcher);
			_locator.RegisterLazySingleton(() => FileSystemWatcher.Object, typeof(IFileSystemWatcher));

			// ISettingsManager
			var sm = new Mock<ISettingsManager>();
			sm.Setup(s => s.Settings).Returns(TestConfig.GenerateSettings());
			_locator.RegisterLazySingleton(() => sm.Object, typeof(ISettingsManager));

			// CrashManager
			_locator.RegisterLazySingleton(() => new Mock<CrashManager>(LogManager.GetCurrentClassLogger()).Object, typeof(CrashManager));

			// IThreadManager
			_locator.RegisterLazySingleton(() => new TestThreadManager(), typeof(IThreadManager));
		}
	}
}
