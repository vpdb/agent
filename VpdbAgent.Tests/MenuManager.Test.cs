using System;
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
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Tests.Mocks;
using Xunit;

namespace VpdbAgent.Tests
{
	public class MenuManager
	{
		[Fact]
		public void ShouldReadInitialGames()
		{
			var pinballXIni = GeneratePinballXIni(new[] {
				@"[VisualPinball]",
				@"Enabled = true",
				@"WorkingPath = C:\Visual Pinball",
				@"Executable = VPinball.exe",
				"Parameters = /play - \"[TABLEPATH]\\[TABLEFILE]\"",
			});
			var fam = new Mock<IFileAccessManager>();
			fam.Setup(f => f.ParseIni(It.IsAny<string>())).Returns(pinballXIni);

			var resolver = TestLocator.Generate();
			var menuManager = new PinballX.MenuManager(
				resolver.GetService<IFileSystemWatcher>(),
				resolver.GetService<ISettingsManager>(),
				fam.Object,
				resolver.GetService<IThreadManager>(), 
				resolver.GetService<CrashManager>(),
				resolver.GetService<Logger>());

			menuManager.Initialize();

			Assert.Equal(1, menuManager.Systems.Count);
		}


		public static IniData GeneratePinballXIni(string[] ini)
		{
			var byteArray = Encoding.UTF8.GetBytes(string.Join("\n", ini));
			var stream = new MemoryStream(byteArray);
			var parser = new FileIniDataParser();
			return parser.ReadData(new StreamReader(stream));
		}
	}

}