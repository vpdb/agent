using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VpdbAgent.Models;
using VpdbAgent.Vpdb.Network;

namespace VpdbAgent
{
	public class GameManager
	{
		public GameManager()
		{
			Game game = new Game();
			game.Id = "Attack from Mars (Bally 1995)";
			game.Filename = "Attack_From_Mars_NIGHT MOD_VP920_v3.5_FS_3-WAY-GI.vpt";

			Platform platform = new Platform();

			platform.Path = @"E:\Pinball\PinballX\Databases\Visual Pinball";
			platform.Name = "Visual Pinball";
			platform.WorkingPath = @"E:\Pinball\Visual Pinball-103";
			platform.TablePath = @"E:\Pinball\Visual Pinball-103\Tables";
			platform.AddGame(game);

			JsonSerializer serializer = new JsonSerializer();
			//			serializer.Converters.Add(new JavaScriptDateTimeConverter());
			serializer.NullValueHandling = NullValueHandling.Ignore;
			serializer.ContractResolver = new SnakeCasePropertyNamesContractResolver();
			serializer.Formatting = Formatting.Indented;

			using (StreamWriter sw = new StreamWriter(@"E:\Pinball\PinballX\Databases\Visual Pinball\vpdb.json"))
			using (JsonWriter writer = new JsonTextWriter(sw)) {
				serializer.Serialize(writer, platform);
			}
		}
	}
}
