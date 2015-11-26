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

		public MessageItemViewModel(Message message)
		{
			Message = message;
		}
	}
}
