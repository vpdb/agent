using System;
using System.Collections.Generic;
using System.Linq;
using Humanizer;
using Refit;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.Application
{
	/// <summary>
	/// Manages the internal messaging system.
	/// 
	/// Any "logworthy" event is logged as a message and appears in the messages
	/// tab of the app. This is particularly important for errors.
	/// </summary>
	public interface IMessageManager
	{
		/// <summary>
		/// Logs that a release has been downloaded successfully.
		/// </summary>
		/// <param name="release">Downloaded release</param>
		/// <param name="version">Version downloaded</param>
		/// <param name="file">File downloaded</param>
		/// <param name="bytesPerSecond">Download speed in bytes per second</param>
		/// <returns>Created message</returns>
		Message LogReleaseDownloaded(VpdbRelease release, VpdbVersion version, VpdbFile file, double bytesPerSecond);

		/// <summary>
		/// Logs that a release has been linked to a local game
		/// </summary>
		/// <param name="game">Local game</param>
		/// <param name="release">Release that has been linked to the game</param>
		/// <param name="fileId">File ID of the file of the release that was linked</param>
		/// <returns>Created message</returns>
		Message LogReleaseLinked(Game game, VpdbRelease release, string fileId);

		/// <summary>
		/// Logs a generic error.
		/// </summary>
		/// <remarks>
		/// Note that running this method also creates an entry a raygun.io,
		/// the crash logger, so you should only log seriouls problems here.
		/// </remarks>
		/// <param name="e">Exception of the error</param>
		/// <param name="message">Message when it happened. Example: "Error downloading file"</param>
		/// <returns>Created message</returns>
		Message LogError(Exception e, string message);

		/// <summary>
		/// Logs an API related error.
		/// </summary>
		/// <param name="e">Exception of the error</param>
		/// <param name="message">Message when it happened. Example: "Error while logging in"</param>
		/// <returns></returns>
		Message LogApiError(ApiException e, string message);

		/// <summary>
		/// Marks all messages as read.
		/// </summary>
		void MarkAllRead();

		/// <summary>
		/// Deletes all messages.
		/// </summary>
		void ClearAll();
	}

	public class MessageManager : IMessageManager
	{
		private readonly IDatabaseManager _databaseManager;
		private readonly CrashManager _crashManager;

		public MessageManager(IDatabaseManager databaseManager, CrashManager crashManager)
		{
			_databaseManager = databaseManager;
			_crashManager = crashManager;
		}

		public Message LogReleaseDownloaded(VpdbRelease release, VpdbVersion version, VpdbFile file, double bytesPerSecond)
		{
			var msg = new Message(MessageType.ReleaseDownloaded, MessageLevel.Info, new Dictionary<string, string> {
				{ DataRelease, release.Id },
				{ DataReleaseName, release.Name },
				{ DataVersion, version.Name },
				{ DataFile, file.Id },
				{ DataSubject, release.Game.DisplayName },
				{ DownloadSpeed, $"{bytesPerSecond.Bytes().ToString("#.0")}/s" },
			});
			return Log(msg);
		}

		public Message LogReleaseLinked(Game game, VpdbRelease release, string fileId)
		{
			var msg = new Message(MessageType.ReleaseLinked, MessageLevel.Info, new Dictionary<string, string> {
				{ DataGameName, game.Id },
				{ DataRelease, release.Id },
				{ DataFile, fileId }
			});
			return Log(msg);
		}

		public Message LogError(Exception exception, string message)
		{
			var msg = CreateError(exception, message, MessageType.Error);
			_crashManager.Report(exception);
			return Log(msg);
		}

		public Message LogApiError(ApiException exception, string message)
		{
			var msg = CreateError(exception, message, MessageType.ApiError);
			msg.Data.Add(DataStatusCode, exception.StatusCode.ToString());
			msg.Data.Add(DataContent, exception.Content);
			_crashManager.Report(exception, "api");
			return Log(msg);
		}

		public void MarkAllRead()
		{
			_databaseManager.GetMessages()
				.Where(msg => !msg.WasRead)
				.ToList()
				.ForEach(msg => msg.WasRead = true);
			//_databaseManager.Save();
		}

		public void ClearAll()
		{
			_databaseManager.ClearLog();
		}

		/// <summary>
		/// Persists a message.
		/// </summary>
		/// <param name="message">Message to add</param>
		/// <returns></returns>
		private Message Log(Message message)
		{
			_databaseManager.Log(message);
			return message;
		}

		/// <summary>
		/// Creates an error message from an exception.
		/// 
		/// Extracts the most-inner exception for the message.
		/// </summary>
		/// <param name="exception">Exception to log</param>
		/// <param name="message">Additional message</param>
		/// <param name="type">Type of the message</param>
		/// <returns>New error message</returns>
		private static Message CreateError(Exception exception, string message, MessageType type)
		{
			var innerException = exception;
			while (innerException.InnerException != null) {
				innerException = innerException.InnerException;
			}
			return new Message(type, MessageLevel.Error, new Dictionary<string, string> {
				{ DataMessage, message },
				{ DataExceptionMessage, exception.Message },
				{ DataInnerExceptionMessage, innerException.Message }
			});
		}

		public const string DataGameName = "game_name";
		public const string DataRelease = "release";
		public const string DataReleaseName = "release_name";
		public const string DataVersion = "version";
		public const string DataFile = "file";
		public const string DataStatusCode = "status_code";
		public const string DownloadSpeed = "download_speed";
		public const string DataContent = "content";
		public const string DataSubject = "subject";
		public const string DataMessage = "message";
		public const string DataExceptionMessage = "exception_message";
		public const string DataInnerExceptionMessage = "inner_exception_message";
	}
}
