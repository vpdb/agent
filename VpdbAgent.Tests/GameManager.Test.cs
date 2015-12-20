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
		public void ShouldInitSuccessfully()
		{
			// setup
			var env = new TestEnvironment();

			var menuManager = env.Locator.GetService<IMenuManager>();
			var gameManager = env.Locator.GetService<IGameManager>();

			// test 
			gameManager.Initialize();

			// assert
			gameManager.Games.ToList().Should().NotBeEmpty().And.HaveCount(env.Menu.Games.Count);

			gameManager.Games[0].Exists.Should().BeTrue("because File.Exists(" + Path.Combine(TestEnvironment.VisualPinballTablePath, gameManager.Games[0].Filename + ".vpt") + " is set up");
			gameManager.Games[0].Filename.Should().Be(menuManager.Systems[0].Games[0].Filename + ".vpt");

			gameManager.Games[1].Exists.Should().BeTrue();
			gameManager.Games[1].Filename.Should().Be(menuManager.Systems[0].Games[1].Filename + ".vpx");

			gameManager.Games[3].Exists.Should().BeFalse("because no File.Exist() is set up");
		}
	}
}
