using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.ViewModels.Games
{
	public class SystemItemViewModel : ReactiveObject
	{
		public PinballXSystem System { get; }
		public bool IsExpanded { get { return _isExpanded; } set { this.RaiseAndSetIfChanged(ref _isExpanded, value); } }
		public double ExpanderRotation => _expanderRotation.Value;

		// commands
		public ReactiveCommand<Unit, Unit> ToggleDetails { get; }


		private readonly ObservableAsPropertyHelper<double> _expanderRotation;
		private bool _isExpanded;
		private readonly GamesViewModel _parent;

		// commands
		public ReactiveCommand<Unit, Unit> SelectResult { get; protected set; }

		

		public SystemItemViewModel(GamesViewModel parent, PinballXSystem system)
		{
			_parent = parent;
			System = system;
			ToggleDetails = ReactiveCommand.Create(() => { IsExpanded = !IsExpanded; });

			this.WhenAnyValue(vm => vm.IsExpanded).Select(expanded => expanded ? 180d : 0d).ToProperty(this, vm => vm.ExpanderRotation, out _expanderRotation);
		}

		public void OnPlatformFilterChanged(string platformName, bool enabled)
		{
			_parent.OnPlatformFilterChanged(platformName, enabled);
		}

		public void OnExecutableFilterChanged(string fileName, bool enabled)
		{
			_parent.OnExecutableFilterChanged(fileName, enabled);
		}
	}
}
