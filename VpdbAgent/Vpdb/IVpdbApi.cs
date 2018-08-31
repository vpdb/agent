using System;
using Refit;
using System.Collections.Generic;
using System.Threading.Tasks;
using VpdbAgent.Vpdb.Models;

namespace VpdbAgent.Vpdb
{
	public interface IVpdbApi
	{
		[Get("/v1/profile")]
		IObservable<VpdbUserFull> GetProfile();

		[Patch("/v1/profile")]
		IObservable<VpdbUserFull> UpdateProfile([Body] VpdbUserFull profile);

		[Get("/v1/games/{id}")]
		IObservable<VpdbGame> GetGame([AliasAs("id")] string gameId);

		[Get("/v1/releases?thumb_format=square&thumb_per_file=1")]
		Task<List<VpdbRelease>> GetReleases();

		[Get("/v1/releases/{id}?thumb_format=square&thumb_per_file=1&full=1")]
		IObservable<VpdbRelease> GetFullRelease([AliasAs("id")] string releaseId);

		[Get("/v1/releases/{id}?thumb_format=square&thumb_per_file=1")]
		IObservable<VpdbRelease> GetRelease([AliasAs("id")] string releaseId);

		[Get("/v1/releases?thumb_format=square&thumb_per_file=1")]
		IObservable<List<VpdbRelease>> GetReleasesBySize([AliasAs("filesize")] long filesize, [AliasAs("threshold")] long threshold);

		[Get("/v1/releases?thumb_format=square&thumb_per_file=1")]
		IObservable<List<VpdbRelease>> GetReleasesByIds([AliasAs("ids")] string releases);
	}
}
