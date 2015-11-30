using System;
using System.Collections.Generic;
using System.Linq;
using Humanizer;
using Refit;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;
using Version = VpdbAgent.Vpdb.Models.Version;

namespace VpdbAgent.Application
{
	public interface IMessageManager
	{
		Message LogReleaseDownloaded(Release release, Version version, FileReference file, double bytesPerSecond);
		Message LogReleaseLinked(Game game, Release release, string fileId);
		Message LogError(Exception e, string message);
		Message LogApiError(ApiException e, string message);
		void MarkAllRead();
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

		public Message LogReleaseDownloaded(Release release, Version version, FileReference file, double bytesPerSecond)
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

		public Message LogReleaseLinked(Game game, Release release, string fileId)
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
			_databaseManager.Database.Messages
				.Where(msg => !msg.WasRead)
				.ToList()
				.ForEach(msg => msg.WasRead = true);
			_databaseManager.Save();
		}

		public void ClearAll()
		{
			_databaseManager.ClearLog();
		}

		private Message Log(Message message)
		{
			_databaseManager.Log(message);
			return message;
		}

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
