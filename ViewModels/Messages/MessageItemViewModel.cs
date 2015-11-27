using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Humanizer;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Download;

namespace VpdbAgent.ViewModels.Messages
{
	public class MessageItemViewModel : ReactiveObject
	{
		// status props
		public Message Message { get; }

		public ObservableCollection<Inline> TextLabel { get { return _textLabel; } set { this.RaiseAndSetIfChanged(ref _textLabel, value); } }

		// privates
		private ObservableCollection<Inline> _textLabel;

		public MessageItemViewModel(Message message)
		{
			Message = message;

			SetupText();
		}

		private void SetupText()
		{
			switch (Message.Type) {
				case MessageType.ReleaseLinked:
					TextLabel = new ObservableCollection<Inline> {
						new Run("Linked game "),
						new Run(Message.Data["game_name"].ToString()) { FontWeight = FontWeights.Bold },
						new Run(" to release "),
						new Run(Message.Data["release"].ToString()) { FontWeight = FontWeights.Bold },
						new Run("."),
					};
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
