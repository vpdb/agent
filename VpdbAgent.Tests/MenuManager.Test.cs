using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using IniParser;
using IniParser.Model;
using Moq;
using NLog;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Common.Filesystem;
using VpdbAgent.PinballX;
using Xunit;

namespace VpdbAgent.Tests
{
	public class MenuManager
	{
		[Fact]
		public void ShouldReadInitialSystem()
		{
			// setup
			var env = new TestEnvironment();
			env.FileAccessManager.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(GeneratePinballXIni(Ini1));
			var menuManager = GetManager(env.Locator);

			// test 
			menuManager.Initialize();

			// assert
			menuManager.Systems.ToList().Should()
				.NotBeEmpty()
				.And.HaveCount(1);
			var system = menuManager.Systems[0];
			system.Name.Should().Be("Visual Pinball");
			system.Executable.Should().Be("VPinball.exe");
			system.Enabled.Should().Be(true);
			system.WorkingPath.Should().Be(@"C:\Visual Pinball");
			system.Parameters.Should().Be("/play - \"[TABLEPATH]\\[TABLEFILE]\"");
		}

		[Fact]
		public void ShouldReadInitialSystems()
		{
			// setup
			var env = new TestEnvironment();
			env.FileAccessManager.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(GeneratePinballXIni(Ini3));
			var menuManager = GetManager(env.Locator);

			// test
			menuManager.Initialize();

			// assert
			menuManager.Systems.ToList().Should()
				.NotBeEmpty()
				.And.HaveCount(3);
			menuManager.Systems.FirstOrDefault(s => s.Name == "Visual Pinball").Should().NotBeNull();
			menuManager.Systems.FirstOrDefault(s => s.Name == "Future Pinball").Should().NotBeNull();
			menuManager.Systems.FirstOrDefault(s => s.Name == "MAME").Should().NotBeNull();
		}

		[Fact]
		public void ShouldUpdateSystems()
		{
			// setup
			var env = new TestEnvironment();
			env.FileAccessManager.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(GeneratePinballXIni(Ini1));
			var menuManager = GetManager(env.Locator);

			// test
			menuManager.Initialize();

			// now, pinballx.ini has changed.
			env.FileAccessManager.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(GeneratePinballXIni(Ini3));
			env.PinballXIniWatcher.OnNext("path-to-pinballx.ini");

			menuManager.Systems.ToList().Should()
				.NotBeEmpty()
				.And.HaveCount(3);
		}

		private static IMenuManager GetManager(IDependencyResolver resolver)
		{
			return new PinballX.MenuManager(
				resolver.GetService<IFileSystemWatcher>(),
				resolver.GetService<ISettingsManager>(),
				resolver.GetService<IFileAccessManager>(),
				resolver.GetService<IThreadManager>(),
				resolver.GetService<IFile>(),
				resolver.GetService<IDirectory>(),
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