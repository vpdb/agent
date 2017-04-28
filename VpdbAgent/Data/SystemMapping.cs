using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VpdbAgent.PinballX;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Data
{
	/// <summary>
	/// Maps data to VPDB. This is the root node.
	/// </summary>
	/// 
	/// <remarks>
	/// When linking local data to VPDB, we need a way of persisting that 
	/// mapping in a transparent way. For that, we create a `vpdb.json` file
	/// in every system folder. Such a file looks something like that:
	/// <code>
	/// {
	///   "mappings": [
	///     {
	///       "filename": "Theatre of magic VPX NZ-TT 1.0.vpx",
	///       "release_id": "e2wm7hdp9b",
	///       "file_id": "skkj298nr8",
	///       "is_synced": false
	///     }
	///   ]
	/// }
	/// </code>
	/// 
	/// This class is used to serialize the above data. Note that for every
	/// system from PinballX, a separate instance of this class is created.
	/// </remarks>
	public class SystemMapping
	{
		/// <summary>
		/// List of mappings
		/// </summary>
		[DataMember] public IReactiveList<Mapping> Mappings { set; get; }

		/// <summary>
		/// Constructor when instantiating self-saving object
		/// </summary>
		/// <param name="path">Path to save</param>
		/// <param name="marshallManager">Marshaller dependency</param>
		public SystemMapping(string path, IMarshallManager marshallManager)
		{
			var mappings = new ReactiveList<Mapping> { ChangeTrackingEnabled = true };
			Observable.Merge(
				mappings.ItemChanged.Select(x => Unit.Default), 
				mappings.ItemsAdded.Select(x => Unit.Default),
				mappings.ItemsRemoved.Select(x => Unit.Default)
			).Sample(TimeSpan.FromSeconds(1)).Subscribe(x => Save(path, marshallManager));
			Mappings = mappings;

			// if mapping goes stale, remove it.
			mappings.ItemChanged.Subscribe(e => {
				var mapping = e.Sender;
				if (mapping.IsStale) {
					mappings.Remove(mapping);
				}
			});
		}

		/// <summary>
		/// Default constructor when serializing
		/// </summary>
		public SystemMapping()
		{
			Mappings = new ReactiveList<Mapping>();
		}

		private void Save(string path, IMarshallManager marshallManager)
		{
			marshallManager.MarshallMappings(this, path);
		}

		public override string ToString()
		{
			return $"[SystemMapping] {Mappings.Count()} mapping(s)";
		}
	}
}
