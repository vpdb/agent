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
		public string CreatedAt { get; private set; }
		public string Icon { get; private set; }

		// privates
		private ObservableCollection<Inline> _textLabel;

		// icons
		private static readonly string WarningIcon = (string)System.Windows.Application.Current.FindResource("IconWarning");
		private static readonly string InfoIcon = (string)System.Windows.Application.Current.FindResource("IconInfoCircle");

		public MessageItemViewModel(Message message)
		{
			Message = message;
			CreatedAt = message.CreatedAt.Humanize(false);
			SetupText();
		}

		private void SetupText()
		{
			switch (Message.Type) {

				case MessageType.ReleaseLinked:
					TextLabel = new ObservableCollection<Inline> {
						new Run("Linked game "),
						new Run(Message.Data[MessageManager.DataGameName]) { FontWeight = FontWeights.Bold },
						new Run(" to release "),
						new Run(Message.Data[MessageManager.DataRelease]) { FontWeight = FontWeights.Bold },
						new Run("."),
					};
					Icon = InfoIcon;
					break;

				case MessageType.ReleaseDownloaded:
					TextLabel = new ObservableCollection<Inline> {
						new Run("Downloaded "),
						new Run(Message.Data[MessageManager.DataReleaseName]) { FontWeight = FontWeights.Bold },
						new Run(" "),
						new Run(Message.Data[MessageManager.DataVersion]) { FontWeight = FontWeights.Bold },
						new Run(" of "),
						new Run(Message.Data[MessageManager.DataSubject]) { FontWeight = FontWeights.Bold },
						new Run(" at "),
						new Run(Message.Data[MessageManager.DownloadSpeed]) { FontWeight = FontWeights.Bold },
						new Run("."),
					};
					Icon = InfoIcon;
					break;

				case MessageType.Error:
					TextLabel = new ObservableCollection<Inline> {
						new Run(Message.Data[MessageManager.DataMessage].ToString()) { FontWeight = FontWeights.Bold }
					};
					Icon = WarningIcon;
					break;

				case MessageType.ApiError:
					TextLabel = new ObservableCollection<Inline> {
						new Run(Message.Data[MessageManager.DataMessage].ToString()) { FontWeight = FontWeights.Bold }
					};
					Icon = WarningIcon;
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
