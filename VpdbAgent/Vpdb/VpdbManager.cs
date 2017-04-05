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
		/// <param name="forceUpdate">If true, don't use cache but force refresh from VPDB API.</param>
		/// <returns>Full release object</returns>
		IObservable<VpdbRelease> GetRelease(string releaseId, bool forceUpdate = false);

		/// <summary>
		/// Returns a game
		/// </summary>
		/// <param name="gameId">ID of the game</param>
		/// <param name="forceUpdate">If true, don't use cache but force refresh from VPDB API.</param>
		/// <returns>Full game object</returns>
		IObservable<VpdbGame> GetGame(string gameId, bool forceUpdate = false);

		/// <summary>
		/// Logs a message and sends it to the crash logger if necessary
		/// </summary>
		/// <param name="e">Exception</param>
		/// <param name="origin">Message, which will prefixed with "Error ", so put someting line "retrieving data"</param>
		void HandleApiError(Exception e, string origin);

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

		public IObservable<VpdbRelease> GetRelease(string releaseId, bool forceUpdate = false)
		{
			if (forceUpdate) {
				return _vpdbClient.Api.GetRelease(releaseId).Do(_databaseManager.SaveRelease);
			}
			var release = _databaseManager.GetRelease(releaseId);
			return release == null 
				? _vpdbClient.Api.GetRelease(releaseId).Do(_databaseManager.SaveRelease) 
				: Observable.Return(release);
		}

		public IObservable<VpdbGame> GetGame(string gameId, bool forceUpdate = false)
		{
			if (forceUpdate) {
				return _vpdbClient.Api.GetGame(gameId).Do(_databaseManager.SaveGame);
			}
			var game = _databaseManager.GetGame(gameId);
			return game == null
				? _vpdbClient.Api.GetGame(gameId).Do(_databaseManager.SaveGame)
				: Observable.Return(game);
		}

		public void HandleApiError(Exception e, string origin)
		{
			_vpdbClient.HandleApiError(e, origin);
		}
	}
}
