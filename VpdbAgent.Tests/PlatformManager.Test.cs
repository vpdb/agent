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
using Xunit;

namespace VpdbAgent.Tests
{
	public class PlatformManager
	{
		[Fact]
		public void ShouldReadInitialPlatform()
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
		}

	}
}
