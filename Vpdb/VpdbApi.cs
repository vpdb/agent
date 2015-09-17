using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Vpdb
{
	interface VpdbApi
	{

		[Get("/api/v1/releases")]
		Task<List<Models.Release>> GetReleases();
	}
}
