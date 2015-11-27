using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.ViewModels.Downloads;
using VpdbAgent.Vpdb.Download;

namespace VpdbAgent.ViewModels.Messages
{
	public class MessagesViewModel : ReactiveObject
	{
		// deps
		private static readonly IMessageManager MessageManager = Locator.CurrentMutable.GetService<IMessageManager>();

		// props
		public IReactiveDerivedList<MessageItemViewModel> Messages { get; }

		public MessagesViewModel()
		{
			Messages = MessageManager.Messages.CreateDerivedCollection(
				msg => new MessageItemViewModel(msg),
				x => true, 
				(x, y) => x.Message.CompareTo(y.Message)
			);
		}
	}
}
