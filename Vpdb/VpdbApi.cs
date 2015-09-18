using Refit;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb
{
	public interface VpdbApi
	{
		[Get("/api/v1/releases?thumb_format=square")]
		Task<List<Models.Release>> GetReleases();
	}
}
