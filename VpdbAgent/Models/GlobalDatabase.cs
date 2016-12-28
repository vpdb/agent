using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet;
using ReactiveUI;
using VpdbAgent.Vpdb;
using VpdbAgent.Vpdb.Download;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Models
{
	/// <summary>
	/// The platform-independent database that is saved as .json in PinballX's
	/// database folder.
	/// </summary>
	public class GlobalDatabase : ReactiveObject
	{
		/// <summary>
		/// A dictionary with releases from VPDB. These serve as cache and are
		/// updated at every application start as well as during runtime through
		/// push messages.
		/// </summary>
		[DataMember]
		[JsonProperty(Order = 1)]
		public Dictionary<string, VpdbRelease> Releases { set; get; } = new Dictionary<string, VpdbRelease>();

		/// <summary>
		/// A dictionary with files from VPDB that are not part of a release, 
		/// like media files or ROMs.
		/// </summary>
		[DataMember]
		[JsonProperty(Order = 0)]
		public Dictionary<string, VpdbFile> Files { set; get; } = new Dictionary<string, VpdbFile>();

		/// <summary>
		/// Contains all download jobs, current and previous, aborted, successful and erroneous.
		/// </summary>
		public ReactiveList<Job> DownloadJobs = new ReactiveList<Job>();

		/// <summary>
		/// Log messages
		/// </summary>
		public ReactiveList<Message> Messages = new ReactiveList<Message>();
			
		// private members
		[DataMember(Name = "jobs")]
		[JsonProperty(Order = 2)]
		private List<Job> _downloadJobs = new List<Job>();

		[DataMember(Name = "messages")]
		[JsonProperty(Order = 3)]
		private List<Message> _messages = new List<Message>();

		public GlobalDatabase()
		{
			DownloadJobs.Changed.Subscribe(_ => { _downloadJobs = DownloadJobs.ToList(); });
			Messages.Changed.Subscribe(_ => { _messages = Messages.ToList(); });
		}

		public GlobalDatabase Update(GlobalDatabase db)
		{
			// ignore if null
			if (db == null) {
				return this;
			}
			if (ReferenceEquals(this, db)) {
				return this;
			}

			Releases = db.Releases;
			Files = db.Files;
			using (Messages.SuppressChangeNotifications()) {
				Messages.RemoveRange(0, Messages.Count);
				Messages.AddRange(db._messages);
			}
			using (DownloadJobs.SuppressChangeNotifications()) {
				DownloadJobs.RemoveRange(0, DownloadJobs.Count);
				DownloadJobs.AddRange(db._downloadJobs);
			}
			return this;
		}

		public override string ToString()
		{
			return $"[Database] {Releases.Count} release(s), {DownloadJobs.Count} download job(s)";
		}
	}
}
