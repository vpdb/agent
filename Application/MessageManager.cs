using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
	}

	public class MessageManager : IMessageManager
	{
		public Message LogReleaseLinked(Game game, Release release, string fileId)
		{
			var msg = new Message(MessageType.ReleaseLinked, new {
				GameName = game.Id,
				Release = release.Id,
				file = fileId
			}, MessageLevel.Info);

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
					return $"Linked release {message.Data.Release} to games {message.Data.GameName}."; 
				default:
					throw new ArgumentOutOfRangeException();
			}
			return null;
		}
	}
}
