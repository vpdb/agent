using System;
using System.Collections.Generic;
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
		string GetText(Message message);
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
			var msg = new Message(MessageType.ReleaseLinked, new {
				GameName = game.Id,
				Release = release.Id,
				file = fileId
			}, MessageLevel.Info);

			_databaseManager.Log(msg);

			return msg;
		}

		public IMessageManager LogError(Exception e, string message)
		{
			throw new NotImplementedException();
		}

		public string GetText(Message message)
		{
			switch (message.Type)
			{
				case MessageType.ReleaseLinked:
					return $"Linked release {message.Data["release"]} to game {message.Data["game_name"]}."; 
                default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
