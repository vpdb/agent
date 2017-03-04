using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using Splat;
using VpdbAgent.Application;
using VpdbAgent.Data.Json;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;
using ILogger = NLog.ILogger;

namespace VpdbAgent.Data.Objects
{
	/// <summary>
	/// PinballX's "system". Note that this entity is never serialized
	/// and resides only in memory.
	/// </summary>
	public class Platform : ReactiveObject
	{
		#region Properties
		/// <summary>
		/// Name of the platform. Serves as ID.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// True if enabled in PinballX.ini, False otherwise.
		/// </summary>
		public bool IsEnabled { get; set; }

		/// <summary>
		/// True if selected in UI, False otherwise.
		/// </summary>
		public bool IsSelected { get; set; } = true;

		/// <summary>
		/// The working path of the executable when launched
		/// </summary>
		public string WorkingPath { get; set; }

		/// <summary>
		/// Folder of the platform's table files
		/// </summary>
		public string TablePath { get; set; }

		/// <summary>
		/// File name of the executable
		/// </summary>
		public string Executable { get; set; }

		/// <summary>
		/// Parameters for the executable to play the table.
		/// </summary>
		/// <example>
		/// /play -"[TABLEPATH]\[TABLEFILE]"
		/// </example>
		public string Parameters { get; set; }

		/// <summary>
		/// Platform type. 
		/// </summary>
		public PlatformType Type { get; set; }

		/// <summary>
		/// Absolute path to database folder.
		/// </summary>
		public string DatabasePath { get; set; }

		/// <summary>
		/// Absolute path to media folder.
		/// </summary>
		public string MediaPath { get; set; }

		/// <summary>
		/// Absolute path to our internal JSON file.
		/// </summary>
		public string DataFile => DatabasePath + @"\vpdb.json";
		#endregion

		// dependencies
		private readonly IDependencyResolver _resolver;
		private readonly IMarshallManager _marshallManager;
		private readonly IThreadManager _threadManager;
		private readonly ILogger _logger;

		/// <summary>
		/// The platform specific data
		/// </summary>
		private readonly PlatformData _data;

		/// <summary>
		/// All attached games
		/// </summary>
		public readonly ReactiveList<Mapping> Mappings = new ReactiveList<Mapping>();
		public readonly Subject<Unit> MappingPropertyChanged = new Subject<Unit>();

		public Platform(PinballXSystem system, IDependencyResolver resolver)
		{
			// deps
			_resolver = resolver;
			_marshallManager = resolver.GetService<IMarshallManager>();
			_threadManager = resolver.GetService<IThreadManager>();
			_logger = resolver.GetService<ILogger>();

			// props
			Name = system.Name;
			IsEnabled = system.Enabled;
			WorkingPath = system.WorkingPath;
			TablePath = system.TablePath;
			Executable = system.Executable;
			Parameters = system.Parameters;
			Type = system.Type;
			DatabasePath = system.DatabasePath;
			MediaPath = system.MediaPath;

			// members
			_data = _marshallManager.UnmarshallPlatformData(DataFile);

			UpdateMappings(system);

			// save changes
			MappingPropertyChanged
				.ObserveOn(Scheduler.Default)
				//.Sample(TimeSpan.FromSeconds(1)) // disable for now, causes timing issues when updating a release (xml gets updated, platform re-parsed, json re-read but json is still the old, non-updated one, resulting in the new version not being displayed.)
				.Subscribe(_ => Save());
		}

		/// <summary>
		/// Saves the current database to json.
		/// </summary>
		/// <returns></returns>
		public Platform Save()
		{
			_data.Mappings = Mappings;
			_marshallManager.MarshallPlatformData(_data, DataFile);
			return this;
		}

		/// <summary>
		/// Updates the games coming from XML files of PinballX.ini
		/// </summary>
		/// <param name="system">System with games attached</param>
		private void UpdateMappings(PinballXSystem system)
		{
			_data.Mappings = MergeMappings(system);
			_threadManager.MainDispatcher.Invoke(delegate {
				using (Mappings.SuppressChangeNotifications()) {
					// todo make this more intelligent by diff'ing and changing instead of drop-and-create
					Mappings.Clear();
					Mappings.AddRange(_data.Mappings);
				}
			});
		}

		/// <summary>
		/// Takes games parsed from the XML database of a system and merges
		/// them with the local .json mappings (and saves the result back to
		/// the .json).
		/// </summary>
		/// <param name="system">System in which the game changed</param>
		private IEnumerable<Mapping> MergeMappings(PinballXSystem system)
		{
			return new List<Mapping>();
			/*if (_data == null) {
				_logger.Warn("No vpdb.json at {0}", DataFile);
				return MergeMappings(system.Games, new List<Mapping>());
			}
			_logger.Info("Found and parsed vpdb.json at {0}", DataFile);
			return MergeMappings(system.Games, _data.Mappings);*/
		}

		/// <summary>
		/// Merges a list of games parsed from an .XML file with a list of 
		/// mappings read from the .json file.
		/// </summary>
		/// <param name="xmlGames">Games read from an .XML file</param>
		/// <param name="jsonMappings">Games read from the internal .json database</param>
		/// <returns>List of merged games</returns>
		private IEnumerable<Mapping> MergeMappings(IEnumerable<PinballXGame> xmlGames, IEnumerable<Mapping> jsonMappings)
		{
			_logger.Info("MergeMappings() START");

			var mappings = new List<Mapping>();
			var enumerableMappings = jsonMappings as Mapping[] ?? jsonMappings.ToArray();
			var enumerableXmlGames = xmlGames as PinballXGame[] ?? xmlGames.ToArray();

			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var xmlGame in enumerableXmlGames) {
				var mapping = enumerableMappings.FirstOrDefault(m => (m.Id.Equals(xmlGame.Description)));
				mappings.Add(mapping == null
					? new Mapping(xmlGame, this, _resolver)
					: mapping.Update(xmlGame, this)
				);
			}

			_logger.Info("MergeMappings() DONE");
			return mappings;
		}

		public override string ToString()
		{
			return $"[Platform] {Name} ({Mappings.Count})";
		}
	}
}
