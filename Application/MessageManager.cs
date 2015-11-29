using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Refit;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.Application
{
	public interface IMessageManager
	{
		Message LogReleaseLinked(Game game, Release release, string fileId);
		Message LogError(Exception e, string message);
		Message LogApiError(ApiException e, string message);
		ReactiveList<Message> Messages { get; }
	}

	public class MessageManager : IMessageManager
	{
		public ReactiveList<Message> Messages { get; }
		private readonly IDatabaseManager _databaseManager;

		public MessageManager(IDatabaseManager databaseManager)
		{
			_databaseManager = databaseManager;
			Messages = databaseManager.Database.Messages;
		}

		public Message LogReleaseLinked(Game game, Release release, string fileId)
		{
			var msg = new Message(MessageType.ReleaseLinked, new Dictionary<string, object> {
				{ DataGameName, game.Id },
				{ DataRelease, release.Id },
				{ DataFile, fileId }
			}, MessageLevel.Info);

			_databaseManager.Log(msg);
			return msg;
		}

		public Message LogError(Exception exception, string message)
		{
			var msg = CreateError(exception, message, MessageType.Error);
			_databaseManager.Log(msg);
			return msg;
		}

		public Message LogApiError(ApiException exception, string message)
		{
			var msg = CreateError(exception, message, MessageType.ApiError);
			msg.Data.Add(DataStatusCode, exception.StatusCode);
			msg.Data.Add(DataContent, exception.Content);

			_databaseManager.Log(msg);
			return msg;
		}

		private static Message CreateError(Exception exception, string message, MessageType type)
		{
			var innerException = exception;
			while (innerException.InnerException != null) {
				innerException = innerException.InnerException;
			}
			return new Message(type, new Dictionary<string, object> {
				{ DataMessage, message },
				{ DataExceptionMessage, exception.Message },
				{ DataInnerExceptionMessage, innerException.Message }
			}, MessageLevel.Error);
		}

		public const string DataGameName = "game_name";
		public const string DataRelease = "release";
		public const string DataFile = "file";
		public const string DataStatusCode = "status_code";
		public const string DataContent = "content";
		public const string DataMessage = "message";
		public const string DataExceptionMessage = "exception_message";
		public const string DataInnerExceptionMessage = "inner_exception_message";
	}
}
