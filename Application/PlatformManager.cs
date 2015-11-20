using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using ReactiveUI;
using VpdbAgent.Models;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Application
{
	public interface IPlatformManager
	{
		/// <summary>
		/// Platforms are 1-way mapped to <see cref="IMenuManager.Systems"/>,
		/// meaning that if systems change (e.g. <c>PinballX.ini</c> is 
		/// manually updated), they are updated but not vice versa.
		/// </summary>
		ReactiveList<Platform> Platforms { get; }

		Platform FindPlatform(TableFile tableFile);
		Platform FindPlatform(TableFile.Platform platform);
	}

	public class PlatformManager : IPlatformManager
	{
		// dependencies
		private readonly IMenuManager _menuManager;
		private readonly IDatabaseManager _databaseManager;
		private readonly Logger _logger;

		// props
		public ReactiveList<Platform> Platforms { get; } = new ReactiveList<Platform>();

		public PlatformManager(IMenuManager menuManager, IDatabaseManager databaseManager, Logger logger)
		{
			_menuManager = menuManager;
			_databaseManager = databaseManager;
			_logger = logger;

			var systems = _menuManager.Systems;

			// populate platforms when system change
			systems.Changed
				.Skip(1)
				.ObserveOn(Scheduler.Default)
				.Subscribe(UpdatePlatforms);

			// populate platform when games change
			systems.Changed
				.ObserveOn(Scheduler.Default)
				.SelectMany(_ => systems
					.Select(system => system.Games.Changed.Select(__ => system))
				.Merge())
			.Subscribe(UpdatePlatform);
		}

		public Platform FindPlatform(TableFile tableFile)
		{
			if (tableFile?.Compatibility == null || tableFile.Compatibility.Count == 0) {
				return null;
			}
			return FindPlatform(tableFile.Compatibility[0].Platform);
		}

		public Platform FindPlatform(TableFile.Platform platform)
		{
			string platformName;
			switch (platform) {
				case TableFile.Platform.VP:
					platformName = "Visual Pinball";
					break;
				case TableFile.Platform.FP:
					platformName = "Future Pinball";
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			return Platforms.FirstOrDefault(p => platformName.Equals(p.Name));
		}

		/// <summary>
		/// Updates platform and games for a given system. <br/>
		/// 
		/// This takes changed data from a system, updates platform and
		/// games, and writes back the result to the json file.
		/// </summary>
		/// <remarks>
		/// Triggered by changes of any of the system's games.
		/// </remarks>
		/// <param name="system"></param>
		private void UpdatePlatform(PinballXSystem system)
		{
			_logger.Info("Updating games for {0}", system);

			// create new platform and find old
			var newPlatform = new Platform(system, _databaseManager.Database);
			var oldPlatform = Platforms.FirstOrDefault(p => p.Name.Equals(system.Name));

			// save vpdb.json for updated platform
			newPlatform.Save();

			// update platforms back on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				using (Platforms.SuppressChangeNotifications()) {
					if (oldPlatform != null) {
						Platforms.Remove(oldPlatform);
					}
					Platforms.Add(newPlatform);
				}
			});
		}

		/// <summary>
		/// Updates all platforms and games. <br/>
		/// 
		/// This takes all available systems, re-creates platforms
		/// and games, and writes back the results to the json files.
		/// </summary>
		/// <remarks>
		/// Triggered by any system changes.
		/// </remarks>
		/// <param name="args"></param>
		private void UpdatePlatforms(NotifyCollectionChangedEventArgs args)
		{
			_logger.Info("Updating all games for all platforms");

			// create platforms from games
			var platforms = _menuManager.Systems.Select(system => new Platform(system, _databaseManager.Database)).ToList();

			// write vpdb.json
			platforms.ForEach(p => p.Save());

			// update platforms back on main thread
			System.Windows.Application.Current.Dispatcher.Invoke(delegate {
				using (Platforms.SuppressChangeNotifications()) {
					// todo make this more intelligent by diff'ing and changing instead of drop-and-create
					Platforms.Clear();
					Platforms.AddRange(platforms);
				}
			});
		}

	}
}
