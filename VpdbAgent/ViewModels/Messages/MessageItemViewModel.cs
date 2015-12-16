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
		public ObservableCollection<Inline> TextLabel { get; private set; }
		public ObservableCollection<Inline> ErrorLabel { get; private set; }
		public Brush Foreground { get; private set; }
		public string CreatedAt { get; private set; }
		public string Icon { get; private set; }
		public bool ShowError { get; private set; }

		// brushes
		private static readonly Brush RedBrush = (Brush)System.Windows.Application.Current.FindResource("LightRedBrush");
		private static readonly Brush GreyBrush = (Brush)System.Windows.Application.Current.FindResource("LabelTextBrush");

		// icons
		private static readonly string WarningIcon = (string)System.Windows.Application.Current.FindResource("IconWarning");
		private static readonly string InfoIcon = (string)System.Windows.Application.Current.FindResource("IconInfoCircle");

		public MessageItemViewModel(Message message)
		{
			Message = message;
			CreatedAt = message.CreatedAt.Humanize(false);
			ShowError = message.Data.ContainsKey(MessageManager.DataInnerExceptionMessage);
			Foreground = message.Level == MessageLevel.Error ? RedBrush : GreyBrush;

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
						new Run(Message.Data[MessageManager.DataMessage]) { FontWeight = FontWeights.Bold }
					};
					ErrorLabel = new ObservableCollection<Inline> {
						new Run(Message.Data[MessageManager.DataInnerExceptionMessage])
					};
					Icon = WarningIcon;
					break;

				case MessageType.ApiError:
					TextLabel = new ObservableCollection<Inline> {
						new Run(Message.Data[MessageManager.DataMessage]) { FontWeight = FontWeights.Bold }
					};
					ErrorLabel = new ObservableCollection<Inline> {
						new Run(Message.Data[MessageManager.DataInnerExceptionMessage])
					};
					Icon = WarningIcon;
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
