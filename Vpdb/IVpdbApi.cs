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

		[Get("/api/v1/releases?thumb_format=square")]
		Task<List<Release>> GetReleases();

		[Get("/api/v1/releases?thumb_format=square")]
		Task<List<Release>> GetReleasesBySize([AliasAs("filesize")] long filesize, [AliasAs("threshold")] long threshold);
	}
}
