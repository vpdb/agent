using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;
using System.Reflection;
using Newtonsoft.Json;
using VpdbAgent.Vpdb.Models;
using VpdbAgent.Vpdb.Network;
using Xunit.Abstractions;

namespace VpdbAgent.Tests.Mocks
{
	public class TestVpdbApi
	{
		public const string AbraCaDabraReleaseId = "w2xts7ooqh";
		public const string AbraCaDabraV20FileId = "w2bm4go1qh";
		public const long AbraCaDabraV20FileSize = 24895488;

		private static readonly JsonSerializer JsonSerializer = new JsonSerializer {
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new SnakeCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};
		private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

		public static IObservable<VpdbRelease> GetAbraCaDabraDetails()
		{
			// https://staging.vpdb.io/api/v1/releases/w2xts7ooqh?thumb_format=square&thumb_per_file=1
			return ReadData<VpdbRelease>("AbraCaDabra_Details");
		}

		public static IObservable<List<VpdbRelease>> GetAbraCaDabraIdentify()
		{
			// https://staging.vpdb.io/api/v1/releases?thumb_format=square&thumb_per_file=1&filesize=24948736&threshold=262144
			return ReadData<List<VpdbRelease>>("AbraCaDabra_Identify");
		}

		private static IObservable<TResult> ReadData<TResult>(string filebase) where TResult : class
		{
			using (var sr = new StreamReader(GetStream(filebase)))
			using (JsonReader reader = new JsonTextReader(sr)) {
				var result = JsonSerializer.Deserialize<TResult>(reader);
				reader.Close();
				return new BehaviorSubject<TResult>(result);
			}
		}

		private static Stream GetStream(string filebase)
		{
			return Assembly.GetManifestResourceStream("VpdbAgent.Tests.Json." + filebase + ".json");
		}
	}
}
