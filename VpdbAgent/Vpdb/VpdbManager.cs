using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using LiteDB;
using VpdbAgent.Application;
using VpdbAgent.Vpdb.Models;
using ILogger = NLog.ILogger;

namespace VpdbAgent.Vpdb
{
	/// <summary>
	/// Manages data from and to VPDB while caching as much as possible.
	/// </summary>
	public interface IVpdbManager
	{
		/// <summary>
		/// Returns a release
		/// </summary>
		/// <param name="releaseId">ID of the release</param>
		/// <returns>Full release object</returns>
		IObservable<VpdbRelease> GetRelease(string releaseId);
	}

	public class VpdbManager : IVpdbManager
	{
		// dependencies
		private readonly IVpdbClient _vpdbClient;
		private readonly IDatabaseManager _databaseManager;
		private readonly ILogger _logger;


		public VpdbManager(IVpdbClient vpdbClient, IDatabaseManager databaseManager, ILogger logger)
		{
			_vpdbClient = vpdbClient;
			_databaseManager = databaseManager;
			_logger = logger;
		}

		public IObservable<VpdbRelease> GetRelease(string releaseId)
		{
			var release = _databaseManager.GetRelease(releaseId);
			return release == null 
				? _vpdbClient.Api.GetRelease(releaseId).Do(_databaseManager.SaveRelease) 
				: Observable.Return(release);
		}
	}
}
