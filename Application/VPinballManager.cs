using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenMcdf;

namespace VpdbAgent.Application
{
	public class VPinballManager
	{

		public string GetTableScript(string path)
		{
			var cf = new CompoundFile(path);
			var storage = cf.RootStorage.GetStorage("GameStg");
			var stream = storage.GetStream("GameData");
			var data = stream.GetData();

			var codeTag = StringToByteArray("04000000434F4445");
			var endbTag = StringToByteArray("04000000454E4442");
			var codeStart = (int)data.IndexOf(codeTag);
			var codeEnd = (int)data.IndexOf(endbTag);

			if (codeStart < 0 || codeEnd < 0) {
				return null;
			}

			const int offset = 12;
			return Encoding.Default.GetString(data.Skip(codeStart + offset).Take(codeEnd - codeStart - offset).ToArray());
		}


		public static byte[] StringToByteArray(string hex)
		{
			return Enumerable.Range(0, hex.Length)
				.Where(x => x % 2 == 0)
				.Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
				.ToArray();
		}
	}


	public static class ExtensionMethods
	{
		public static Int64 IndexOf(this Byte[] value, Byte[] pattern)
		{
			if (value == null)
				throw new ArgumentNullException("value");

			if (pattern == null)
				throw new ArgumentNullException("pattern");

			Int64 valueLength = value.LongLength;
			Int64 patternLength = pattern.LongLength;

			if ((valueLength == 0) || (patternLength == 0) || (patternLength > valueLength))
				return -1;

			Int64[] badCharacters = new Int64[256];

			for (Int64 i = 0; i < 256; ++i)
				badCharacters[i] = patternLength;

			Int64 lastPatternByte = patternLength - 1;

			for (Int64 i = 0; i < lastPatternByte; ++i)
				badCharacters[pattern[i]] = lastPatternByte - i;

			// Beginning

			Int64 index = 0;

			while (index <= (valueLength - patternLength)) {
				for (Int64 i = lastPatternByte; value[(index + i)] == pattern[i]; --i) {
					if (i == 0)
						return index;
				}

				index += badCharacters[value[(index + lastPatternByte)]];
			}

			return -1;
		}
	}
}
