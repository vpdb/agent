using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using Splat;
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
			env.MarshallManager.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(TestEnvironment.GeneratePinballXIni(Ini1));
			var menuManager = env.Locator.GetService<IMenuManager>();

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
			env.MarshallManager.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(TestEnvironment.GeneratePinballXIni(Ini3));
			var menuManager = env.Locator.GetService<IMenuManager>();

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
			env.MarshallManager.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(TestEnvironment.GeneratePinballXIni(Ini1));
			var menuManager = env.Locator.GetService<IMenuManager>();

			// test
			menuManager.Initialize();

			// now, pinballx.ini has changed.
			env.MarshallManager.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(TestEnvironment.GeneratePinballXIni(Ini3));
			env.PinballXIniWatcher.OnNext("path-to-pinballx.ini");

			menuManager.Systems.ToList().Should()
				.NotBeEmpty()
				.And.HaveCount(3);
		}

		[Fact]
		public void ShouldParseGames()
		{
			// setup
			var env = new TestEnvironment();
			var dbPath = Path.Combine(env.Settings.PbxFolder, "Databases", "Visual Pinball");

			env.Directory.Setup(d => d.Exists(dbPath)).Returns(true);
			env.Directory.Setup(d => d.GetFiles(dbPath)).Returns(new []{ "Visual Pinball.xml" });

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