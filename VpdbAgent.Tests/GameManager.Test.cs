using System;
using System.IO;
using System.Linq;
using System.Reactive.Threading.Tasks;
using FluentAssertions;
using Moq;
using NLog;
using NLog.Config;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Models;
using VpdbAgent.PinballX;
using VpdbAgent.Tests.Mocks;
using VpdbAgent.ViewModels.Games;
using VpdbAgent.Vpdb.Models;
using Xunit;
using Xunit.Abstractions;
using Xunit.NLog.Targets;

namespace VpdbAgent.Tests
{
	public class GameManager : BaseTest
	{
		public GameManager(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Fact]
		public void ShouldInitSuccessfully()
		{
			// setup
			var env = new TestEnvironment(Logger);

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
		public void ShouldLinkRelease()
		{
			// setup
			var env = new TestEnvironment(Logger);
			var gameManager = env.Locator.GetService<IGameManager>();

			// test 
			gameManager.Initialize();
			var game = gameManager.Games[0];
			TestVpdbApi.GetAbraCaDabraDetails().Subscribe(release =>
			{
				gameManager.LinkRelease(game, release, TestVpdbApi.AbraCaDabraV20FileId);

				// assert
				game.HasRelease.Should().BeTrue();
			});
		}

		[Fact]
		public void ShouldIdentifyGameInstantly()
		{
			// setup
			var env = new TestEnvironment(Logger);
			var gameManager = env.Locator.GetService<IGameManager>();

			// test 
			gameManager.Initialize();

			// let's mock also IGameManager, we only need to know if LinkRelease is called.
			var gameManagerMock = env.Register<IGameManager>();
			var game = gameManager.Games[0];
			var viewModel = new GameItemViewModel(game, env.Locator);

			viewModel.IdentifyRelease.Execute(null);

			// assert
			gameManagerMock.Verify(gm => gm.LinkRelease(
				It.Is<Game>(g => g.Id == game.Id),
				It.Is<VpdbRelease>(r => r.Id == TestVpdbApi.AbraCaDabraReleaseId),
				TestVpdbApi.AbraCaDabraV20FileId
			));
		}

		[Fact]
		public void ShouldIdentifyMultipleVersions()
		{
			// setup
			var env = new TestEnvironment(Logger);
			var gameManager = env.Locator.GetService<IGameManager>();
			env.Menu.Games[0].Filename = "not_same_name";

			// test 
			gameManager.Initialize();

			var game = gameManager.Games[0];
			var viewModel = new GameItemViewModel(game, env.Locator);

			viewModel.IdentifyRelease.Execute(null);

			// assert
			viewModel.IdentifiedReleases.Should().HaveCount(2);
		}
	}
}
