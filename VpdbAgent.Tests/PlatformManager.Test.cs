using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.PinballX;
using VpdbAgent.Vpdb.Models;
using Xunit;

namespace VpdbAgent.Tests
{
	public class PlatformManager
	{
		[Fact]
		public void ShouldReadInitialPlatformsAndGames()
		{
			// setup
			var env = new TestEnvironment();

			var menuManager = env.Locator.GetService<IMenuManager>();
			var platformManager = env.Locator.GetService<IPlatformManager>();

			// test 
			menuManager.Initialize();

			// assert
			platformManager.Platforms.ToList().Should().NotBeEmpty().And.HaveCount(1);
			var platform = platformManager.Platforms[0];
			platform.Name.Should().Be("Visual Pinball");
			platform.Games.Should().NotBeEmpty().And.HaveCount(env.Menu.Games.Count);
		}

		[Fact]
		public void ShouldRetrieveCorrectPlatform()
		{
			// setup
			var env = new TestEnvironment();

			var menuManager = env.Locator.GetService<IMenuManager>();
			var platformManager = env.Locator.GetService<IPlatformManager>();

			env.MarshallManager.Setup(f => f.ParseIni(TestEnvironment.PinballXIniPath)).Returns(TestEnvironment.GetPinballXIni(Ini2));

			// test 
			menuManager.Initialize();

			// assert
			platformManager.FindPlatform(VpdbTableFile.VpdbPlatform.VP).Should().NotBeNull();
			platformManager.FindPlatform(VpdbTableFile.VpdbPlatform.FP).Should().NotBeNull();
			platformManager.Invoking(p => p.FindPlatform(VpdbTableFile.VpdbPlatform.Unknown)).ShouldThrow<ArgumentOutOfRangeException>();
		}

		private static readonly string[] Ini2 = {
			@"[VisualPinball]",
			@"Enabled = true",
			@"WorkingPath = C:\Visual Pinball",
			@"Executable = VPinball.exe",
			"Parameters = /play - \"[TABLEPATH]\\[TABLEFILE]\"",

			@"[FuturePinball]",
			@"Enabled = true",
			@"WorkingPath = C:\Future Pinball",
			@"Executable = FuturePinball.exe",
			"Parameters = /open \"[TABLEPATH]\\[TABLEFILE]\" /play /exit /arcaderender"
		};
	}
}
