using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;
using System.Text;
using IniParser;
using IniParser.Model;
using Moq;
using NLog;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.Models;
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
		public readonly Mock<IMarshallManager> MarshallManager = new Mock<IMarshallManager>();

		// observers
		public readonly Subject<string> PinballXIniWatcher = new Subject<string>();
		public readonly Subject<string> DatabaseWatcher = new Subject<string>();
		public readonly Subject<string> TableWatcher = new Subject<string>();

		private readonly ModernDependencyResolver _locator;

		public TestEnvironment()
		{
			_locator = new ModernDependencyResolver();

			// IFileAccessManager
			MarshallManager.Setup(f => f.ParseIni(PinballXIniPath)).Returns(GetPinballXIni(_ini));
			MarshallManager.Setup(f => f.UnmarshallPlatformDatabase(VisualPinballDatabasePath)).Returns(new PlatformDatabase());
			_locator.RegisterLazySingleton(() => MarshallManager.Object, typeof(IMarshallManager));

			// IFileSystemWatcher
			FileSystemWatcher.Setup(f => f.FileWatcher(PinballXIniPath)).Returns(PinballXIniWatcher);
			FileSystemWatcher.Setup(f => f.DatabaseWatcher(It.IsAny<string>(), It.IsAny<IList<PinballXSystem>>())).Returns(DatabaseWatcher);
			FileSystemWatcher.Setup(f => f.TablesWatcher(It.IsAny<IList<PinballXSystem>>())).Returns(TableWatcher);
			_locator.RegisterLazySingleton(() => FileSystemWatcher.Object, typeof(IFileSystemWatcher));

			// ISettingsManager
			var sm = new Mock<ISettingsManager>();
			sm.Setup(s => s.Settings).Returns(Settings);
			_locator.RegisterLazySingleton(() => sm.Object, typeof(ISettingsManager));

			// IFile, IDirectory
			_locator.RegisterLazySingleton(() => File.Object, typeof(IFile));
			_locator.RegisterLazySingleton(() => Directory.Object, typeof(IDirectory));

			// Logging
			_locator.RegisterLazySingleton(LogManager.GetCurrentClassLogger, typeof(Logger));
			_locator.RegisterLazySingleton(() => new Mock<CrashManager>(LogManager.GetCurrentClassLogger()).Object, typeof(CrashManager));

			// IThreadManager
			_locator.RegisterLazySingleton(() => new TestThreadManager(), typeof(IThreadManager));

			// IMenuManager
			_locator.RegisterLazySingleton(() => new PinballX.MenuManager(
				_locator.GetService<IFileSystemWatcher>(),
				_locator.GetService<ISettingsManager>(),
				_locator.GetService<IMarshallManager>(),
				_locator.GetService<IThreadManager>(),
				_locator.GetService<IFile>(),
				_locator.GetService<IDirectory>(),
				_locator.GetService<Logger>()), typeof(IMenuManager));

			// IPlatformManager
			_locator.RegisterLazySingleton(() => new Application.PlatformManager(
				_locator.GetService<IMenuManager>(),
				_locator.GetService<IThreadManager>(),
				_locator.GetService<Logger>(),
				_locator
			), typeof(IPlatformManager));
		}

		public const string PinballXPath              = @"C:\PinballX";
		public const string PinballXIniPath           = @"C:\PinballX\Config\PinballX.ini";
		public const string VisualPinballPath         = @"C:\Visual Pinball";
		public const string VisualPinballTablePath    = @"C:\Visual Pinball\Tables";
		public const string VisualPinballDatabasePath = @"C:\PinballX\Databases\Visual Pinball\vpdb.json";

		public Settings Settings = new Settings {
			PbxFolder = PinballXPath
		};

		private readonly string[] _ini = {
			@"[VisualPinball]",
			@"Enabled = true",
			@"WorkingPath = " + VisualPinballPath,
			@"TablePath = " + VisualPinballTablePath,
			@"Executable = VPinball.exe",
			"Parameters = /play - \"[TABLEPATH]\\[TABLEFILE]\"",
		};

		public static IniData GetPinballXIni(string[] ini)
		{
			var byteArray = Encoding.UTF8.GetBytes(string.Join("\n", ini));
			var stream = new MemoryStream(byteArray);
			var parser = new FileIniDataParser();
			return parser.ReadData(new StreamReader(stream));
		}
	}
}
