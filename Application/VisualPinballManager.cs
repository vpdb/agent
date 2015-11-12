using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HashLib;
using NLog;
using OpenMcdf;

namespace VpdbAgent.Application
{
	/// <summary>
	/// A class that knows how to read and write VPT files.
	/// </summary>
	/// <remarks>
	/// Visual Pinball's file format is what's called a "OLE Compound Document", 
	/// which more or less represents a file system within a file, used in 
	/// Microsoft products decades ago.
	/// 
	/// Basically, every file contains two "storage" nodes, `GameStg` (game 
	/// storage) and `TableInfo`. While `TableInfo` only contains data the user 
	/// can set in the table info dialog, `GameStg` contains the actual data. 
	/// Within GameStg, we find the following "streams":
	/// 
	///   - GameData
	///   - GameItem{N} (playfield data)
	///   - Image{N} (images)
	///   - Sound{N} (sounds)
	///   - Collection{N}
	///   - CustomInfoTags
	///   - Version
	///   - MAC (the checksum)
	/// 
	/// All of these contain binary data in another structure called "BIFF" 
	/// (nothing to do with the Excel format). No idea where this brainfuck comes
	/// from, but generally it contains blocks that have the following structure:
	/// 
	///   - [4 bytes] size of block (blockSize)
	///   - [blockSize bytes] data, which is:
	///       - [4 bytes] tag name
	///       - [blockSize - 4 bytes] real data
	/// 
	/// GameData is the stream that also contains the table script (under the
	/// "CODE" tag).
	/// 
	/// When changing data in the table, the hash must be re-calculated, 
	/// otherwise VP refuses to open it ("data corrupted"). Since the table 
	/// script is part of the hash, we must re-compute the entire hash when 
	/// changing the script.
	/// 
	/// Hashing means collecting data everywhere in the file. Apart from images
	/// and sounds, everything is hashed. The hash function is MD2. See 
	/// <see cref="ComputeChecksum"/> for more details.
	/// 
	/// The implemented algorithm in this class is not fail-safe, because the 
	/// VP team could at any point add additional data to be hashed. Therefore, 
	/// when changing the table script, we first calculate the current hash before
	/// applying the change and validate against the stored hash (the one in the 
	/// MAC stream).
	/// 
	/// </remarks>
	public interface IVisualPinballManager
	{
		/// <summary>
		/// Computes the checksum of a given VTP file.
		/// </summary>
		/// <param name="path">Path to VPT file</param>
		/// <returns>Checksum</returns>
		byte[] ComputeChecksum(string path);
	}

	public class VisualPinballManager : IVisualPinballManager
	{
		// deps
		private readonly Logger _logger;

		public VisualPinballManager(Logger logger)
		{
			_logger = logger;
		}

		public string GetTableScript(string path)
		{
			var cf = new CompoundFile(path);
			var storage = cf.RootStorage.GetStorage("GameStg");

			var stream = storage.GetStream("GameData");
			byte[] data;
			try {
				data = stream.GetData();
			} catch (CFItemNotFound) {
				_logger.Warn("Cannot get table script because GameData was not found in GameStg.");
				return null;
			}

			var codeTag = HexToBytes("04000000434F4445"); // CODE
			var endbTag = HexToBytes("04000000454E4442"); // ENDB
			var codeStart = (int)data.IndexOf(codeTag);
			var codeEnd = (int)data.IndexOf(endbTag);

			if (codeStart < 0 || codeEnd < 0) {
				return null;
			}

			const int offset = 12;
			return Encoding.Default.GetString(data.Skip(codeStart + offset).Take(codeEnd - codeStart - offset).ToArray());
		}

		public byte[] ComputeChecksum(string path)
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			_logger.Info("Reading file from {0}", path);

			// load up the file through our OLE Compound lib
			var cf = new CompoundFile(path);
			var storage = cf.RootStorage.GetStorage("GameStg");
			var info = cf.RootStorage.GetStorage("TableInfo");

			// a list of byte arrays that will be flattened and hashed
			var hashBuf = new List<byte[]>();
			var stats = new TableStats();

			// retrieve data to hash from table file
			_logger.Info("Adding streams...");
			hashBuf.Add(Encoding.Default.GetBytes("Visual Pinball")); // for whatever reason, the hash data start with this constant
			AddStream(hashBuf, "Version", storage);
			AddStream(hashBuf, "TableName", info);
			AddStream(hashBuf, "AuthorName", info);
			AddStream(hashBuf, "TableVersion", info);
			AddStream(hashBuf, "ReleaseDate", info);
			AddStream(hashBuf, "AuthorEmail", info);
			AddStream(hashBuf, "AuthorWebSite", info);
			AddStream(hashBuf, "TableBlurb", info);
			AddStream(hashBuf, "TableDescription", info);
			AddStream(hashBuf, "TableRules", info);
			AddStream(hashBuf, "Screenshot", info);
			AddCustomStreams(hashBuf, "CustomInfoTags", storage, info, stats);
			AddBiffData(hashBuf, "GameData", storage, 0, stats);
			AddStreams(hashBuf, "GameItem", storage, stats.NumSubObjects, 4, stats);
			AddStreams(hashBuf, "Collection", storage, stats.NumCollections, 0, stats);

			// now we have collected the hash data, flatten it
			var hashBytes = hashBuf.SelectMany(list => list).ToArray();
			stopwatch.Stop();
			_logger.Info("Hash data collected in {0} ms, {1} bytes.", stopwatch.ElapsedMilliseconds, hashBytes.Length);

			// and hash it
			stopwatch.Restart();
			var hash = HashFactory.Crypto.CreateMD2();
			var result = hash.ComputeBytes(hashBytes);

			_logger.Info("Hash       = {0} ({1} ms)", BitConverter.ToString(result.GetBytes()), stopwatch.ElapsedMilliseconds);
			_logger.Info("Hash (MAC) = {0}", BitConverter.ToString(storage.GetStream("MAC").GetData()));

			return result.GetBytes();
		}

		/// <summary>
		/// VPT files offer a way to add custom info through the table info 
		/// dialog. These are key/value pairs that are probably useful 
		/// somewhere.
		/// 
		/// The *keys* are stored in the `CustomInfoTags` stream of `GameStg`. The
		/// *values* are separate streams in the `TableInfo` storage.
		/// 
		/// Since those are also hashed, we need to obtain them and loop through 
		/// them.
		/// </summary>
		/// <param name="hashBuf">Current data to hash</param>
		/// <param name="streamName">Stream name where the keys are stored</param>
		/// <param name="stg">Storage where the keys are stored</param>
		/// <param name="info">Storage where the values are stored</param>
		/// <param name="stats">Stats collector</param>
		private void AddCustomStreams(ICollection<byte[]> hashBuf, string streamName, CFStorage stg, CFStorage info, TableStats stats)
		{
			// retrieve keys
			var keyBlocks = AddBiffData(hashBuf, streamName, stg, 0, stats);
			var keys = keyBlocks.Select(block => Encoding.Default.GetString(block.Data.Take(4).ToArray())).ToList();

			// read stream for every key
			_logger.Info("Reading all blocks in {0}: {1}", streamName, string.Join(", ", keys));
			keys.ForEach(key => { AddStream(hashBuf, key, info); });
		}

		/// <summary>
		/// Adds a complete stream to the hash data.
		/// </summary>
		/// <param name="hashBuf">Current data to hash</param>
		/// <param name="streamName">Stream to hash</param>
		/// <param name="stg">Storage of the stream</param>
		private void AddStream(ICollection<byte[]> hashBuf, string streamName, CFStorage stg)
		{
			try {
				var stream = stg.GetStream(streamName);
				if (stream != null) {
					var data = stream.GetData();
					hashBuf.Add(data);
				}
			} catch (CFItemNotFound) {
				_logger.Warn("Skipping non-existent Stream {0}.", streamName);

			} catch (Exception e) {
				_logger.Error(e, "Error reading data!");
			}
		}

		/// <summary>
		/// Adds a collection of streams to the hash data.
		/// 
		/// GameItems and Collections are also hashed, but they are separate streams
		/// numbered from 1 to n. This assumes they are sequentially numbered and
		/// adds them all to the current data to hash.
		/// </summary>
		/// <param name="hashBuf">Current data to hash</param>
		/// <param name="streamName">Prefix of the streams to hash</param>
		/// <param name="stg">Storage of the streams</param>
		/// <param name="count">Number of streams to hash</param>
		/// <param name="offset">Offset where to start reading the BIFF data</param>
		/// <param name="stats">Stats collector</param>
		private void AddStreams(ICollection<byte[]> hashBuf, string streamName, CFStorage stg, int count, int offset, TableStats stats)
		{
			_logger.Info("Adding {0} {1}s...", count, streamName);
			for (var n = 0; n < count; n++) {
				AddBiffData(hashBuf, streamName + n, stg, offset, stats);
			}
		}

		/// <summary>
		/// Adds the data of all BIFF blocks to the hash data.
		/// </summary>
		/// <param name="hashBuf">Current data to hash</param>
		/// <param name="streamName">Name of the stream where BIFF data is stored</param>
		/// <param name="stg">Storage of the stream</param>
		/// <param name="offset">Byte offset to start reading BIFF data</param>
		/// <param name="stats">Stats collector</param>
		/// <returns>List of parsed BIFF blocks</returns>
		private List<BiffBlock> AddBiffData(ICollection<byte[]> hashBuf, string streamName, CFStorage stg, int offset, TableStats stats)
		{
			// init result
			var blocks = new List<BiffBlock>();

			// get stream from compound document
			var stream = stg.GetStream(streamName);
			if (stream == null) {
				_logger.Warn("No stream {0} in provided storage!", streamName);
				return blocks;
			}

			// get data from stream
			byte[] buf;
			try {
				buf = stream.GetData();
			} catch (CFItemNotFound) {
				_logger.Warn("No data in stream {0}.", streamName);
				return blocks;
			}

			// loop through BIFF blocks
			var i = offset;
			do {
				// Usually, we have:
				//
				//   [4 bytes] size of block              | `blockSize` - not hashed
				//   [blockSize bytes] data, which is:    | `block`     - hashed
				//       [4 bytes] tag name               | `tag`
				//       [blockSize - 4 bytes] real data  | `data`
				//
				// In case of a string, real data is again prefixed with 4 bytes 
				// of string size, but we don't care because those are hashed too.
				//
				// What's NOT hashed is the original block size or the stream block 
				// size, see below.
				//
				var blockSize = BitConverter.ToInt32(buf, i);
				var block = buf.Skip(i + 4).Take(blockSize).ToArray(); // contains tag and data
				var tag = Encoding.Default.GetString(block.Take(4).ToArray());

				// treat exceptions where we hash differently than usual
				if (tag == "FONT") {

					// Not hashed, but need to find out how many bytes to skip. Best guess: tag
					// is followed by 8 bytes of whatever, then 2 bytes size BE, followed by
					// data.
					blockSize = BitConverter.ToInt16(buf
						.Skip(i + 17)
						.Take(2)
						.Reverse() // convert to big endian
						.ToArray(), 0
						);
					// fonts are ignored, so just update the pointer and continue
					i += 15;

				} else if (tag == "CODE") {

					// In here, the data starts with 4 size bytes again. This is a special case,
					// what's hashed now is only the tag and the data *after* the 4 size bytes.
					// concretely, we have:
					//
					//   [4 bytes] size of block | `blockSize` above
					//   [4 bytes] tag name      | `tag`
					//   [4 bytes] size of code  | `blockSize` below
					//   [n bytes] code          | `block` below
					//
					i += 8;
					blockSize = BitConverter.ToInt32(buf, i);
					_logger.Info("Code is {0} bytes long.", blockSize);

					block = buf.Skip(i + 4).Take(blockSize).ToArray();
					block = Encoding.Default.GetBytes(tag).Concat(block).ToArray();
				}

				// parse data block
				if (blockSize > 4) {
					var data = block.Skip(4).ToArray();
					blocks.Add(new BiffBlock(tag, data));
					CollectStats(tag, data, stats);
				}
				i += blockSize + 4;

				// finally, add block to hash data
				hashBuf.Add(block);

			} while (i < buf.Length - 4);

			return blocks;
		}

		/// <summary>
		/// In order to hash all the `GameItem{N}` and `Collection{N}` streams,
		/// we need to know the number of streams for each type.
		/// 
		/// These can be found in the BIFF data of the `GameData` stream. This method
		/// saves them into an object that can be later used.
		/// </summary>
		/// <param name="tag">Current tag of the BIFF data</param>
		/// <param name="data">Data of the BIFF data</param>
		/// <param name="stats">Stats object for result</param>
		private static void CollectStats(string tag, byte[] data, TableStats stats)
		{
			if (data == null) {
				return;
			}
			switch (tag) {
				case "SEDT":
					if (stats.NumSubObjects < 0) {
						stats.NumSubObjects = BitConverter.ToInt32(data, 0);
					}
					break;
				case "SSND":
					if (stats.NumSounds < 0) {
						stats.NumSounds = BitConverter.ToInt32(data, 0);
					}
					break;
				case "SIMG":
					if (stats.NumTextures < 0) {
						stats.NumTextures = BitConverter.ToInt32(data, 0);
					}
					break;
				case "SFNT":
					if (stats.NumFonts < 0) {
						stats.NumFonts = BitConverter.ToInt32(data, 0);
					}
					break;
				case "SCOL":
					if (stats.NumCollections < 0) {
						stats.NumCollections = BitConverter.ToInt32(data, 0);
					}
					break;
			}
		}

		/// <summary>
		/// Converts a hex string to a byte array.
		/// </summary>
		/// <param name="hex">String in hex format (without "0x")</param>
		/// <returns>Byte array</returns>
		private static byte[] HexToBytes(string hex)
		{
			return Enumerable.Range(0, hex.Length)
				.Where(x => x % 2 == 0)
				.Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
				.ToArray();
		}

		/// <summary>
		/// An object wrapping data and tag of the BIFF structure into an object.
		/// </summary>
		private class BiffBlock
		{
			private readonly string _tag;
			public readonly byte[] Data;

			public BiffBlock(string tag, byte[] data)
			{
				_tag = tag;
				Data = data;
			}

			public override string ToString()
			{
				return $"{_tag} ({Data.Length} bytes)";
			}
		}

		/// <summary>
		/// An object holding information about the streams of the game storage.
		/// </summary>
		private class TableStats
		{
			public int NumSubObjects = -1;
			public int NumSounds = -1;
			public int NumTextures = -1;
			public int NumFonts = -1;
			public int NumCollections = -1;
		}
	}

	/// <summary>
	/// A fast find function that looks for bytes in an byte array.
	/// </summary>
	public static class ExtensionMethods
	{
		public static long IndexOf(this byte[] value, byte[] pattern)
		{
			if (value == null) {
				throw new ArgumentNullException(nameof(value));
			}

			if (pattern == null) {
				throw new ArgumentNullException(nameof(pattern));
			}

			var valueLength = value.LongLength;
			var patternLength = pattern.LongLength;

			if ((valueLength == 0) || (patternLength == 0) || (patternLength > valueLength)) {
				return -1;
			}

			var badCharacters = new long[256];

			for (long i = 0; i < 256; ++i) {
				badCharacters[i] = patternLength;
			}

			var lastPatternByte = patternLength - 1;

			for (long i = 0; i < lastPatternByte; ++i) {
				badCharacters[pattern[i]] = lastPatternByte - i;
			}

			long index = 0;

			while (index <= (valueLength - patternLength)) {
				for (var i = lastPatternByte; value[(index + i)] == pattern[i]; --i) {
					if (i == 0) {
						return index;
					}
				}
				index += badCharacters[value[(index + lastPatternByte)]];
			}

			return -1;
		}
	}
}
