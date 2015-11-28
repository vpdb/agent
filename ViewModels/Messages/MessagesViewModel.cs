using System.Reactive.Linq;
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
		
		// output props
		private readonly ObservableAsPropertyHelper<bool> _isEmpty;
		public bool IsEmpty => _isEmpty.Value;

		public MessagesViewModel()
		{
			Messages = MessageManager.Messages.CreateDerivedCollection(
				msg => new MessageItemViewModel(msg),
				x => true, 
				(x, y) => x.Message.CompareTo(y.Message)
			);

			Messages.CountChanged
				.Select(_ => Messages.Count == 0)
				.StartWith(Messages.Count == 0)
				.ToProperty(this, x => x.IsEmpty, out _isEmpty);
		}
	}
}
