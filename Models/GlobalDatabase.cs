using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VpdbAgent.Vpdb;
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
		public Dictionary<string, Release> Releases { set; get; } = new Dictionary<string, Release>();
		[DataMember]
		private List<DownloadJob> _downloadJobs = new List<DownloadJob>();

		/// <summary>
		/// Contains all download jobs, current and previous, aborted, successful and erroneous.
		/// </summary>
		public ReactiveList<DownloadJob> DownloadJobs = new ReactiveList<DownloadJob>();

		public GlobalDatabase()
		{
			DownloadJobs.Changed.Subscribe(_ => { _downloadJobs = DownloadJobs.ToList(); });
		}

		public GlobalDatabase Update(GlobalDatabase db)
		{
			// ignore if null
			if (db == null) {
				return this;
			}
			Releases = db.Releases;
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
