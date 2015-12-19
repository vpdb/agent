using System.IO;
using System.Text;
using IniParser;
using IniParser.Model;
using Moq;
using NLog;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.PinballX;
using VpdbAgent.Tests.Mocks;
using Xunit;

namespace VpdbAgent.Tests
{
	public class MenuManager
	{
		[Fact]
		public void ShouldReadInitialSystem()
		{
			// setup
			var fam = new Mock<IFileAccessManager>();
			fam.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(GeneratePinballXIni(Ini1));

			var menuManager = GetManager(new TestDependencies().Locator, fam.Object);

			// test 
			menuManager.Initialize();

			Assert.Equal(1, menuManager.Systems.Count);
		}

		[Fact]
		public void ShouldReadInitialSystems()
		{
			// setup
			var fam = new Mock<IFileAccessManager>();
			fam.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(GeneratePinballXIni(Ini3));

			var menuManager = GetManager(new TestDependencies().Locator, fam.Object);

			// test
			menuManager.Initialize();

			Assert.Equal(3, menuManager.Systems.Count);
		}

		[Fact]
		public void ShouldUpdateSystems()
		{
			// setup
			var fam = new Mock<IFileAccessManager>();
			fam.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(GeneratePinballXIni(Ini1));

			var deps = new TestDependencies();
			var menuManager = GetManager(deps.Locator, fam.Object);

			// test
			menuManager.Initialize();

			// now, pinballx.ini has changed.
			fam.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(GeneratePinballXIni(Ini3));
			deps.PinballXIniWatcher.OnNext("path-to-pinballx.ini");

			Assert.Equal(3, menuManager.Systems.Count);
		}

		private static IMenuManager GetManager(IDependencyResolver resolver, IFileAccessManager fam)
		{
			return new PinballX.MenuManager(
				resolver.GetService<IFileSystemWatcher>(),
				resolver.GetService<ISettingsManager>(),
				fam,
				resolver.GetService<IThreadManager>(),
				resolver.GetService<CrashManager>(),
				resolver.GetService<Logger>());
		}

		private static IniData GeneratePinballXIni(string[] ini)
		{
			var byteArray = Encoding.UTF8.GetBytes(string.Join("\n", ini));
			var stream = new MemoryStream(byteArray);
			var parser = new FileIniDataParser();
			return parser.ReadData(new StreamReader(stream));
		}

		private static readonly string[] Ini1 = {
			@"[VisualPinball]",
			@"Enabled = true",
			@"WorkingPath = C:\Visual Pinball",
			@"Executable = VPinball.exe",
			"Parameters = /play - \"[TABLEPATH]\\[TABLEFILE]\"",
		};

		private static readonly string[] Ini3 = {
			@"[VisualPinball]",
			@"Enabled = true",
			@"WorkingPath = C:\Visual Pinball",
			@"Executable = VPinball.exe",
			"Parameters = /play - \"[TABLEPATH]\\[TABLEFILE]\"",

			@"[FuturePinball]",
			@"Enabled = true",
			@"WorkingPath = C:\Future Pinball",
			@"Executable = FuturePinball.exe",
			"Parameters = /open \"[TABLEPATH]\\[TABLEFILE]\" /play /exit /arcaderender",

			@"[System_1]",
			@"Name = MAME",
			@"Enabled = true",
			@"WorkingPath = C:\Emulators\MAME",
			@"Executable = mamep64.exe",
			"Parameters =[TABLEFILE]",
		};
	}

}