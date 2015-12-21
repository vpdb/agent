using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;
using System.Reflection;
using Newtonsoft.Json;
using VpdbAgent.Vpdb.Models;
using VpdbAgent.Vpdb.Network;

namespace VpdbAgent.Tests.Mocks
{
	public class TestVpdbApi
	{
		private static readonly JsonSerializer JsonSerializer = new JsonSerializer {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new SnakeCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};
		private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

		public static IObservable<List<VpdbRelease>> GetReleasesBySize()
		{
			using (var sr = new StreamReader(GetStream("AbraCaDabra_Identify")))
			using (JsonReader reader = new JsonTextReader(sr)) {
				var result = JsonSerializer.Deserialize<List<VpdbRelease>>(reader);
				reader.Close();
				var o = new BehaviorSubject<List<VpdbRelease>>(null);
				o.OnNext(result);
				return o;
			}
		}

		private static Stream GetStream(string filebase)
		{
			return Assembly.GetManifestResourceStream("VpdbAgent.Tests.Resources." + filebase + ".json");
		}
	}
}
