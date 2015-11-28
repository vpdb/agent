using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Models;
using Game = VpdbAgent.Models.Game;

namespace VpdbAgent.Application
{
	public interface IMessageManager
	{
		Message LogReleaseLinked(Game game, Release release, string fileId);
		IMessageManager LogError(Exception e, string message);
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
				{ "game_name", game.Id },
				{ "release", release.Id },
				{ "file", fileId }
			}, MessageLevel.Info);

			_databaseManager.Log(msg);
			return msg;
		}

		public IMessageManager LogError(Exception e, string message)
		{
			throw new NotImplementedException();
		}
	}
}
