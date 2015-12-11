using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Mindscape.Raygun4Net;
using Mindscape.Raygun4Net.Messages;
using NLog;
using NLog.Targets;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Application
{
	/// <summary>
	/// Reports crashes to Raygun, our crash reporter.
	/// 
	/// The application log is sent as custom data.
	/// </summary>
	/// <remarks>
	/// Note that in here should be the only place where Raygun is referenced.
	/// </remarks>
	public class CrashManager
	{
		private readonly Logger _logger;
		private readonly MemoryTarget _log;
		private readonly RaygunClient _raygun = new RaygunClient("rDGC5mT6YBc77sU8bm5/Jw==");

		public CrashManager(Logger logger)
		{
			_logger = logger;
			_log = LogManager.Configuration.FindTargetByName<MemoryTarget>("memory");
		}

		/// <summary>
		/// Sends an exception to the crash reporter
		/// </summary>
		/// <param name="exception">Exception to send</param>
		/// <returns>This instance</returns>
		public CrashManager Report(Exception exception)
		{
			_raygun.SendInBackground(exception, null, GetLogs());
			return this;
		}

		/// <summary>
		/// Sends an exception along with a tag to the crash reporter.
		/// </summary>
		/// <param name="exception">Exception to send</param>
		/// <param name="tag">Tag to apply</param>
		/// <returns>This instance</returns>
		public CrashManager Report(Exception exception, string tag)
		{
			IList<string> tags = new List<string>() { tag };
			_raygun.SendInBackground(exception, tags, GetLogs());
			return this;
		}

		/// <summary>
		/// Adds user info the crash logger.
		/// </summary>
		/// <param name="user">Logged user</param>
		/// <returns>This instance</returns>
		public CrashManager SetUser(VpdbUserFull user)
		{
			_raygun.UserInfo = new RaygunIdentifierMessage(user.Id) {
				IsAnonymous = false,
				FullName = user.Name,
				Email = user.Email
			};
			return this;
		}


		/// <summary>
		/// Retrieves the application log as a string wrapped in dictionary.
		/// </summary>
		/// <returns>Application log</returns>
		private Dictionary<string, object> GetLogs()
		{
			var logs = string.Join("\n", _log.Logs);
			return new Dictionary<string, object>() {{"log", logs }};
		}

		/// <summary>
		/// A callback for non-caught exceptions. 
		/// 
		/// Only reports to the crash logger in release.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			_logger.Error(e.Exception, "Uncatched error!");
#if !DEBUG
			_raygun.Send(e.Exception, null, GetLogs());
#endif
		}
	}

}
