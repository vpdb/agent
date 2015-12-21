using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Models;
using VpdbAgent.PinballX;
using VpdbAgent.Tests.Mocks;
using VpdbAgent.ViewModels.Games;
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

		[Fact]
		public void ShouldIdentifyGame()
		{
			// setup
			var env = new TestEnvironment();
			var gameManager = env.Locator.GetService<IGameManager>();
			//env.VpdbApi.Setup(v => v.GetReleasesBySize(24895488, GameItemViewModel.MatchThreshold)).Returns(TestVpdbApi.GetReleasesBySize());

			// test 
			gameManager.Initialize();

			var game = gameManager.Games[0];
			var viewModel = new GameItemViewModel(game, env.Locator);


			viewModel.IdentifyRelease.Execute(null);
		}
	}
}
