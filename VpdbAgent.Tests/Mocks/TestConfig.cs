using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IniParser;
using IniParser.Model;
using Moq;
using VpdbAgent.Application;

namespace VpdbAgent.Tests.Mocks
{
	public class TestConfig
	{
		public static IniData GeneratePinballXIni()
		{
			string[] ini = {
				@"[VisualPinball]",
				@"Enabled = true",
				@"WorkingPath = C:\Visual Pinball",
				@"Executable = VPinball.exe",
				"Parameters = /play - \"[TABLEPATH]\\[TABLEFILE]\"",
			};
			var byteArray = Encoding.UTF8.GetBytes(string.Join("\n", ini));
			var stream = new MemoryStream(byteArray);
			var parser = new FileIniDataParser();
			return parser.ReadData(new StreamReader(stream));
		}

		public static Settings GenerateSettings()
		{
			var settings = new Settings
			{
				PbxFolder = @"C:\PinballX"
			};
			return settings;
		}

	}
}
