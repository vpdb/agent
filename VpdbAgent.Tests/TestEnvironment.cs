using System.Collections.Generic;
using System.Reactive.Subjects;
using Moq;
using NLog;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Tests.Mocks;

namespace VpdbAgent.Tests
{
	public class TestEnvironment
	{
		public IDependencyResolver Locator => _locator;

		// mocks
		public readonly Mock<IFile> File = new Mock<IFile>();
		public readonly Mock<IDirectory> Directory = new Mock<IDirectory>();
		public readonly Mock<IFileSystemWatcher> FileSystemWatcher = new Mock<IFileSystemWatcher>();
		public readonly Mock<IFileAccessManager> FileAccessManager = new Mock<IFileAccessManager>();

		// observers
		public readonly Subject<string> PinballXIniWatcher = new Subject<string>();
		public readonly Subject<string> DatabaseWatcher = new Subject<string>();
		public readonly Subject<string> TableWatcher = new Subject<string>();

		private readonly ModernDependencyResolver _locator;

		public TestEnvironment()
		{
			_locator = new ModernDependencyResolver();
	
			// IFileAccessManager
			_locator.RegisterLazySingleton(() => FileAccessManager.Object, typeof(IFileAccessManager));

			// IFileSystemWatcher
			FileSystemWatcher.Setup(f => f.FileWatcher(It.IsAny<string>())).Returns(PinballXIniWatcher);
			FileSystemWatcher.Setup(f => f.DatabaseWatcher(It.IsAny<string>(), It.IsAny<IList<PinballXSystem>>())).Returns(DatabaseWatcher);
			FileSystemWatcher.Setup(f => f.TablesWatcher(It.IsAny<IList<PinballXSystem>>())).Returns(TableWatcher);
			_locator.RegisterLazySingleton(() => FileSystemWatcher.Object, typeof(IFileSystemWatcher));

			// ISettingsManager
			var sm = new Mock<ISettingsManager>();
			sm.Setup(s => s.Settings).Returns(TestConfig.GenerateSettings());
			_locator.RegisterLazySingleton(() => sm.Object, typeof(ISettingsManager));

			// IFile, IDirectory
			_locator.RegisterLazySingleton(() => File.Object, typeof(IFile));
			_locator.RegisterLazySingleton(() => Directory.Object, typeof(IDirectory));

			// Logging
			_locator.RegisterLazySingleton(LogManager.GetCurrentClassLogger, typeof(Logger));
			_locator.RegisterLazySingleton(() => new Mock<CrashManager>(LogManager.GetCurrentClassLogger()).Object, typeof(CrashManager));

			// IThreadManager
			_locator.RegisterLazySingleton(() => new TestThreadManager(), typeof(IThreadManager));
		}
	}
}
