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
		IObservable<UserFull> GetProfile();

		[Patch("/api/v1/user")]
		IObservable<UserFull> UpdateProfile([Body] UserFull profile);

		[Get("/api/v1/games/{id}")]
		IObservable<Game> GetGame([AliasAs("id")] string gameId);

		[Get("/api/v1/releases?thumb_format=square&thumb_per_file=1")]
		Task<List<Release>> GetReleases();

		[Get("/api/v1/releases/{id}?thumb_format=square&thumb_per_file=1&full=1")]
		IObservable<Release> GetFullRelease([AliasAs("id")] string releaseId);

		[Get("/api/v1/releases/{id}?thumb_format=square&thumb_per_file=1")]
		IObservable<Release> GetRelease([AliasAs("id")] string releaseId);

		[Get("/api/v1/releases?thumb_format=square&thumb_per_file=1")]
		IObservable<List<Release>> GetReleasesBySize([AliasAs("filesize")] long filesize, [AliasAs("threshold")] long threshold);

		[Get("/api/v1/releases?thumb_format=square&thumb_per_file=1")]
		IObservable<List<Release>> GetReleasesByIds([AliasAs("ids")] string releases);
	}
}
