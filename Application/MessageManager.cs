using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Application
{
	public interface IMessageManager
	{
		IMessageManager LogError(Exception e, string message);
	}

	public class MessageManager : IMessageManager
	{
		public IMessageManager LogError(Exception e, string message)
		{
			throw new NotImplementedException();
		}
	}
}
