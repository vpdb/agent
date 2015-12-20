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
	public class GameManager
	{
		[Fact]
		public void ShouldReadInitialPlatformsAndGames()
		{
			// setup
			var env = new TestEnvironment();

			var menuManager = env.Locator.GetService<IMenuManager>();
			var platformManager = env.Locator.GetService<IPlatformManager>();
			var gameManager = env.Locator.GetService<IGameManager>();

			// test 
			gameManager.Initialize();
		}
	}
}
