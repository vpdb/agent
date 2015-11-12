using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HashLib;
using NLog;
using OpenMcdf;

namespace VpdbAgent.Application
{
	/// <summary>
	/// A class that knows how to read and write VPT files.
	/// </summary>
	/// <remarks>
	/// Visual Pinball's file format is what's called "OLE Compound Documents", 
	/// which more or less represents a file system within a file, used in 
	/// Microsoft products decades ago.
	/// 
	/// Basically, every file contains two "storage" nodes, "GameStg" (game storage)
	/// and "TableInfo". While TableInfo only contains data user can set in the
	/// table info dialog, GameStg contains the actual data. Within GameStg, we 
	/// find the following "streams":
	/// 
	///   - GameData (BIFF - more on that below)
	///   - GameItem{N} (playfield data)
	///   - Image{N} (images)
	///   - Sound{N} (sounds)
	///   - Collection{N}
	///   - CustomInfoTags (BIFF)
	///   - Version
	///   - MAC (the checksum)
	/// 
	/// All of these contain binary data in another structure called "BIFF" 
	/// (nothing to do with the Excel format). No idea where this brainfuck comes
	/// from, but basically it contains blocks that have the following structure:
	/// 
	///   - 4 bytes size of block
	///   - 4 bytes tag name
	///   - (block size - 4) bytes data
	/// 
	/// GameData is the stream that also contains the table script (under the
	/// "CODE" tag).
	/// 
	/// When changing data in the table, the hash must be re-calculated, 
	/// otherwise VP refuses to open it (data corrupted). Since the table script
	/// is part of the hash, we must re-compute the entire hash when changing
	/// the script.
	/// 
	/// Hashing means collecting data a little bit everywhere in the file 
	/// (about 10% of data is hashed) and running it through a MD2 generator.
	/// See <see cref="ComputeTableHash"/> for more details.
	/// 
	/// The implemented algorithm is not fail-safe, because the VP team could
	/// at any point add additional data to be hashed. Therefore, when changing
	/// the table script, this class firstly calculates the hash before the 
	/// change and validates it against the stored hash.
	/// 
	/// </remarks>
	public interface IVisualPinballManager
	{
		void ComputeTableHash(string path);
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
			byte[] data = null;
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

		public void ComputeTableHash(string path)
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
			AddBlocks(hashBuf, "CustomInfoTags", storage, info, stats);
			LoadBiff(hashBuf, "GameData", storage, 0, stats);
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
		}

		/// <summary>
		/// VPT files offer a way to add custom info through the table info 
		/// dialog. These are key/value pairs that are probably useful 
		/// somewhere.
		/// 
		/// The *keys* are stored in the `CustomInfoTags` stream of `GameStg`. The
		/// *values* are separate streams in the `TableInfo` storage.
		/// 
		/// Since this is also hashed, we need to obtain and loop through them.
		/// </summary>
		/// <param name="hashBuf">Current data to hash</param>
		/// <param name="streamName">Stream name where the keys are stored</param>
		/// <param name="stg">Storage where the keys are stored</param>
		/// <param name="info">Storage where the values are stored</param>
		/// <param name="stats">Stats collector</param>
		private void AddBlocks(ICollection<byte[]> hashBuf, string streamName, CFStorage stg, CFStorage info, TableStats stats)
		{
			// retrieve keys
			var keyBlocks = LoadBiff(hashBuf, streamName, stg, 0, stats);
			var keys = keyBlocks.Select(block => Encoding.Default.GetString(block.data.Take(4).ToArray())).ToList();

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
					//_logger.Info("Adding {0} bytes to hash for stream {1}", data.Length, streamName);
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
		/// <param name="stats"></param>
		private void AddStreams(ICollection<byte[]> hashBuf, string streamName, CFStorage stg, int count, int offset, TableStats stats)
		{
			_logger.Info("Adding {0} {1}s...", count, streamName);
			for (var n = 0; n < count; n++) {
				LoadBiff(hashBuf, streamName + n, stg, offset, stats);
			}
		}

		private List<BiffBlock> LoadBiff(ICollection<byte[]> hashBuf, string streamName, CFStorage stg, int offset, TableStats stats)
		{
			var blocks = new List<BiffBlock>();

			//_logger.Info("Parsing stream {0}", streamName);
			var stream = stg.GetStream(streamName);
			if (stream == null) {
				_logger.Warn("No stream {0} in provided storage!", streamName);
				return blocks;
			}
			var buf = stream.GetData();
			//_logger.Info("Got {0} bytes from stream {1}", buf.Length, streamName);

			var i = offset;

			do {
				/* usually, we have:
				 *    4 bytes size of block (blockSize)
				 *    blockSize bytes of data, where data is
				 *        4 bytes tag name
				 *        (blockSize - 4) bytes data
				 *
				 *    in case of a string, data is again prefixed with 4 bytes of string size,
				 *    but we don't care because those are hashed too.
				 *
				 *    what's NOT hashed is the original block size or the stream block size,
				 *    see below
				 */
				var blockSize = BitConverter.ToInt32(buf, i); // blockSize = buf.slice(i, i + 4).readInt32LE(0);  // size of the block excluding the 4 size bytes
				var block = buf.Skip(i + 4).Take(blockSize).ToArray(); //  block = buf.slice(i + 4, i + 4 + blockSize);     // contains tag and data
				var tag = Encoding.Default.GetString(block.Take(4).ToArray()); // tag = block.slice(0, 4).toString();

				byte[] data = null;
				//_logger.Info("Found block {0} of {1} bytes", tag, blockSize);

				// this switch is "correcting" block data for CODE and FONT
				switch (tag) {

					// ignored
					case "FONT":
						/* not hashed, but need to find out how many bytes to skip. best guess: tag
						 * is followed by 8 bytes of whatever, then 2 bytes size BE, followed by
						 * data.
						 */
						blockSize = BitConverter.ToInt16(buf // blockSize = buf.readInt16BE(i + 17);
							.Skip(i + 17)
							.Take(2)
							.Reverse() // convert to big endian
							.ToArray(), 0);


						// fonts are ignored, so just update the pointer and continue
						i += 19 + blockSize;
						break;

					// streams
					case "CODE":

						/* in here, the data starts with 4 size bytes again. this is a special case,
						 * what's hashed now is only the tag and the data *after* the 4 size bytes.
						 * concretely, we have:
						 *    4 bytes size of block (blockSize above)
						 *    4 bytes tag name (tag)
						 *    4 bytes size of code (blockSize below)
						 *    n bytes of code (block below)
						 */
						i += 8;
						blockSize = BitConverter.ToInt32(buf, i); // blockSize = buf.slice(i, i + 4).readInt32LE(0);
						_logger.Info("Code is {0} bytes long.", blockSize);

						block = buf.Skip(i + 4).Take(blockSize).ToArray();      // block = buf.slice(i + 4, i + 4 + blockSize);
						block = Encoding.Default.GetBytes(tag).Concat(block).ToArray(); //  block = Buffer.concat([new Buffer(tag), block]);
						break;
				}

				// add new block
				if (!tag.Equals("FONT")) {
					if (blockSize > 4) {
						data = block.Skip(4).ToArray();
						//_logger.Info("Adding tag {0} of {1} bytes", tag, data.Length);
						blocks.Add(new BiffBlock(tag, data));
					} else {
						//_logger.Info("Skipping empty tag {0}", tag);
					}
					i += blockSize + 4;
				}

				if (data != null) {
					switch (tag) {
						case "SEDT":
							if (stats.NumSubObjects < 0) {
								stats.NumSubObjects = BitConverter.ToInt32(data, 0); // data.readInt32LE(0);
							}
							break;
						case "SSND":
							if (stats.NumSounds < 0) {
								stats.NumSounds = BitConverter.ToInt32(data, 0); // data.readInt32LE(0);
							}
							break;
						case "SIMG":
							if (stats.NumTextures < 0) {
								stats.NumTextures = BitConverter.ToInt32(data, 0); // data.readInt32LE(0);
							}
							break;
						case "SFNT":
							if (stats.NumFonts < 0) {
								stats.NumFonts = BitConverter.ToInt32(data, 0); // data.readInt32LE(0);
							}
							break;
						case "SCOL":
							if (stats.NumCollections < 0) {
								stats.NumCollections = BitConverter.ToInt32(data, 0); // data.readInt32LE(0);
							}
							break;
					}
				}
				//console.log('*** Adding block [%d] %s', blockSize, block);
				//_logger.Info("*** Adding {0} bytes to hash", block.Length);
				hashBuf.Add(block);

				//				var blk = block.length > 16 ? block.slice(0, 16) : block;
				//				console.log('*** Added block %s %s (%d / %d bytes): %s | %s', tag, makeHash().toString('hex'), blockSize, hashSize, blk.toString('hex'), blk);
				//process.stdout.write(tag + " ");

			} while (i < buf.Length - 4);
			//_logger.Info("*** Done parsing {0}", streamName);
			return blocks;
		}

		private string MakeHash(IEnumerable<byte[]> data)
		{
			var hashBytes = data.SelectMany(list => list).ToArray();
			var hash = HashFactory.Crypto.CreateMD2();
			var result = hash.ComputeBytes(hashBytes);
			_logger.Info("Hashed {0} bytes.", hashBytes.Length);
			return BitConverter.ToString(result.GetBytes());
		}

		private static byte[] HexToBytes(string hex)
		{
			return Enumerable.Range(0, hex.Length)
				.Where(x => x % 2 == 0)
				.Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
				.ToArray();
		}

		private class BiffBlock
		{
			public readonly string tag;
			public readonly byte[] data;

			public BiffBlock(string tag, byte[] data)
			{
				this.tag = tag;
				this.data = data;
			}
		}

		private class TableStats
		{
			public int NumSubObjects = -1;
			public int NumSounds = -1;
			public int NumTextures = -1;
			public int NumFonts = -1;
			public int NumCollections = -1;
		}
	}

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

			// Beginning

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
