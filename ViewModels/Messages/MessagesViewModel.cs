using System;
using System.Reactive.Linq;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;

namespace VpdbAgent.ViewModels.Messages
{
	public class MessagesViewModel : ReactiveObject
	{
		// deps
		private static readonly IDatabaseManager DatabaseManager = Locator.CurrentMutable.GetService<IDatabaseManager>();

		// props
		public IReactiveDerivedList<MessageItemViewModel> Messages { get; }
		
		// output props
		private readonly ObservableAsPropertyHelper<bool> _isEmpty;
		public bool IsEmpty => _isEmpty.Value;

		public MessagesViewModel()
		{
			Messages = DatabaseManager.Database.Messages.CreateDerivedCollection(
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
