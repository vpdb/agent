using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using Xunit;
using Xunit.Abstractions;
using Xunit.NLog.Targets;

namespace VpdbAgent.Tests
{
	public abstract class BaseTest : IDisposable
	{
		protected readonly ILogger Logger;

		protected BaseTest(ITestOutputHelper outputHelper)
		{
			var config = new LoggingConfiguration();
			var testTarget = new TestOutputTarget { Layout = @"${date:format=HH\:mm\:ss} ${message}" };
			config.AddTarget("test", testTarget);
			config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, testTarget));
			LogManager.Configuration = config;

			Logger = outputHelper.GetNLogLogger();
		}

		public void Dispose()
		{
			Logger.RemoveTestOutputHelper();
		}
	}
}
