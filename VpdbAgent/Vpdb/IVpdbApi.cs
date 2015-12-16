using System;
using Refit;
using System.Collections.Generic;
using System.Threading.Tasks;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Vpdb
{
	public interface IVpdbApi
	{
		[Get("/api/v1/user")]
		IObservable<VpdbUserFull> GetProfile();

		[Patch("/api/v1/user")]
		IObservable<VpdbUserFull> UpdateProfile([Body] VpdbUserFull profile);

		[Get("/api/v1/games/{id}")]
		IObservable<VpdbGame> GetGame([AliasAs("id")] string gameId);

		[Get("/api/v1/releases?thumb_format=square&thumb_per_file=1")]
		Task<List<VpdbRelease>> GetReleases();

		[Get("/api/v1/releases/{id}?thumb_format=square&thumb_per_file=1&full=1")]
		IObservable<VpdbRelease> GetFullRelease([AliasAs("id")] string releaseId);

		[Get("/api/v1/releases/{id}?thumb_format=square&thumb_per_file=1")]
		IObservable<VpdbRelease> GetRelease([AliasAs("id")] string releaseId);

		[Get("/api/v1/releases?thumb_format=square&thumb_per_file=1")]
		IObservable<List<VpdbRelease>> GetReleasesBySize([AliasAs("filesize")] long filesize, [AliasAs("threshold")] long threshold);

		[Get("/api/v1/releases?thumb_format=square&thumb_per_file=1")]
		IObservable<List<VpdbRelease>> GetReleasesByIds([AliasAs("ids")] string releases);
	}
}
