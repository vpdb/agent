using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using Splat;

namespace VpdbAgent.VisualPinball
{
	/// <summary>
	/// The BIFF format seems to be some VPinball-internal storage format.
	/// 
	/// It is unknown why the "OLE Compound Document" format was not used for
	/// storing all data. Instead, it only serves as wrapper for binary streams
	/// which are each serialized as BIFF.
	/// 
	/// BIFF seems to be a key/value format. Keys and values are simply 
	/// concatenated after each other, so there could be very well duplicate
	/// keys. However, looking at VP's code, that doesn't seem to be a valid
	/// scenario.
	/// 
	/// Most of the data written as BIFF is structured as follows:
	/// 
	///   - [4 bytes] size of block
	///   - [block size bytes] data, which is:
	///       - [4 bytes] tag name
	///       - [blockSize - 4 bytes] real data (if any)
	/// 
	/// This is valid for primitives and most structs. However, sometimes just
	/// a tag is written and the data behind is completely unstructured. It is 
	/// then up to VP's deserializer to correctly parse that data and increment
	/// the pointer accordingly.
	/// 
	/// For example, the table script in `GameData` is written like so:
	///   
	///    bw.WriteTag(FID(CODE));
	///    m_pcv->SaveToStream(pstm, hcrypthash, CheckPermissions(DISABLE_SCRIPT_EDITING) ? hcryptkey : NULL);
	/// 
	/// `WriteTag()` just writes "size 4", followed by "CODE", with no further
	/// data. Then, the CodeViewer object just dumps whatever it thinks is right
	/// into the stream, without even going through the BiffWriter, which is
	/// supposed to abtract all this shit.
	/// 
	/// Looking at the result we can guess that it writes first the size of the
	/// table script, followed by its data. But other objects are less obvious,
	/// e.g. image data when saving a texture:
	/// 
	///    bw.WriteTag(FID(BITS));
	///    LZWWriter lzwwriter(pstream, (int *)m_pdsBuffer->data(), m_width * 4, m_height, m_pdsBuffer->pitch());
	/// 
	/// Reading back looks like this:
	/// 
	///    LZWReader lzwreader(pbr->m_pistream, (int *)m_pdsBuffer->data(), m_width * 4, m_height, m_pdsBuffer->pitch());
	/// 
	/// So lzwreader.ccp is the only class that knows how to read the data
	/// block after BITS, and even worse, it's the only class that knows
	/// where to set the pointer after finishing reading.
	/// 
	/// We only need to understand how to write `GameData`, which up to date
	/// contains only one of these cases: `CODE`. In order to serialize
	/// correctly, we use the <see cref="IBiffTagSerializer"/> interface, 
	/// which delivers the correct block size depending on the tag name and 
	/// must be provided for `CODE`.
	/// 
	/// </summary>
	public class BiffSerializer
	{
		private static readonly Logger Logger = Locator.CurrentMutable.GetService<Logger>();

		/// <summary>
		/// The entire BIFF data block to handle
		/// </summary>
		private readonly byte[] _biffData;

		/// <summary>
		/// An ordered list of tags so we can easily serialize back later.
		/// </summary>
		private readonly List<string> _tags = new List<string>();

		/// <summary>
		/// A dictionary with byte blocks linked to tags for fast access
		/// </summary>
		private readonly Dictionary<string, byte[]> _blocks = new Dictionary<string, byte[]>();

		/// <summary>
		/// A list of parsers that handle all unstructured tags
		/// </summary>
		private Dictionary<string, IBiffTagSerializer> _parsers;

		/// <summary>
		/// Construct with BIFF data
		/// </summary>
		/// <param name="biffData"></param>
		public BiffSerializer(byte[] biffData)
		{
			_biffData = biffData;
		}

		/// <summary>
		/// Returns true if the given tag has been read, or false otherwise.
		/// Run this before accessing any data.
		/// </summary>
		/// <param name="tag">Tag to check for</param>
		/// <returns>Boolean indicating if tag has been read</returns>
		public bool ContainsTag(string tag)
		{
			return _blocks.ContainsKey(tag);
		}

		/// <summary>
		/// Returns the data as a string
		/// </summary>
		/// <param name="tag">Tag under which the data is stored</param>
		/// <returns>Data parsed as string</returns>
		public string GetString(string tag)
		{
			return Encoding.Default.GetString(_blocks[tag]);
		}

		/// <summary>
		/// Sets a new string value to a given tag
		/// </summary>
		/// <param name="tag">Tag under which the data is stored</param>
		/// <param name="value">New string value of the tag</param>
		public void SetString(string tag, string value)
		{
			_blocks[tag] = Encoding.Default.GetBytes(value);
		}

		/// <summary>
		/// Returns the data as integer
		/// </summary>
		/// <param name="tag">Tag under which the data is stored</param>
		/// <returns>Data parsed as integer</returns>
		public int GetInt(string tag)
		{
			return BitConverter.ToInt32(_blocks[tag], 0);
		}

		/// <summary>
		/// Parses the provided data into block accessible by tag name.
		/// </summary>
		/// <param name="parsers">List of parsers to handle unstructured data</param>
		public void Deserialize(Dictionary<string, IBiffTagSerializer> parsers)
		{
			_parsers = parsers;
			var i = 0;
			do {
			
				var blockSize = BitConverter.ToInt32(_biffData, i);
				var block = _biffData.Skip(i + 4).Take(blockSize).ToArray(); // contains tag and data
				var tag = Encoding.Default.GetString(block.Take(4).ToArray());
				byte[] data;

				// Standard case:
				//
				//   [4 bytes] size of block              | `blockSize` | i
				//   [blockSize bytes] data, which is:    | `block`     | i + 4
				//       [4 bytes] tag name               | `tag`       | i + 4
				//       [blockSize - 4 bytes] real data  | `data`      | i + 8
				if (!parsers.ContainsKey(tag)) {

					if (blockSize <= 4 && !tag.Equals("ENDB")) {
						Logger.Warn("Just reading a tag without data. IUnstructured parser missig?");
					}
					data = block.Skip(4).ToArray();

				// Special case:
				//
				//   [4 bytes] size of block              | 4      | i
				//   [blockSize bytes] data, which is:    | `tag`  | i + 4
				//       [4 bytes] tag name               | `tag`  | i + 4
				//   [who the fuck knows] data            | `data` | i + 8
				} else {
					if (blockSize > 4) {
						Logger.Warn("I have data after a unstructured tag. Probably wrong?");
					}
					var parser = parsers[tag];
					var size = parser.GetSize(_biffData, i + 8);
					data = parser.DeserializeData(_biffData, i + 8, size);
					i += size;
				}

				// add to block and tag list
				if (_blocks.ContainsKey(tag)) {
					Logger.Warn("D'oh, tag \"{0}\" exists already!", tag);
				} else {
					_blocks.Add(tag, data);
				}
				_tags.Add(tag);
				i += blockSize + 4;

			} while (i < _biffData.Length - 4);
		}

		/// <summary>
		/// Serializes previously parsed data back to the BIFF format
		/// </summary>
		/// <returns>Serialized BIFF Data</returns>
		public byte[] Serialize()
		{
			return _tags.SelectMany(tag =>
			{
				var block = _blocks[tag];
				if (!_parsers.ContainsKey(tag)) {
					return BitConverter.GetBytes(block.Length + 4)  // size of block
						.Concat(Encoding.Default.GetBytes(tag))     // tag name
						.Concat(block);                             // data
				}
				var parser = _parsers[tag];
				return BitConverter.GetBytes(4)                     // size of block (always 4)
					.Concat(Encoding.Default.GetBytes(tag))         // tag name (then we would be done)
					.Concat(parser.SerializeData(block));           // data block from parser
			}).ToArray();
		}

		/// <summary>
		/// Serializes BIFF data and compares the result with the original data
		/// and returns true on success.
		/// </summary>
		/// <remarks>
		/// Make sure you change values *after* running this method.
		/// </remarks>
		/// <returns>True if we can assume that we can safely serialize previously deserialized data, false otherwise.</returns>
		public bool CheckConsistency()
		{
			var serializedData = Serialize();
			Logger.Info("Comparing original data ({0} bytes) with re-serialized data ({1} bytes).", _biffData.Length, serializedData.Length);
			return _biffData.SequenceEqual(serializedData);
		}

		/// <summary>
		/// A parser that is able to determine the size of a binary block
		/// by looking at the binary block and return its data. It is also
		/// capable of correctly re-serializing the data.
		/// </summary>
		public interface IBiffTagSerializer
		{
			/// <summary>
			/// Returns the size of the block
			/// </summary>
			/// <param name="biffData">Entire BIFF data block</param>
			/// <param name="offset">Offset where block to analyize starts</param>
			/// <returns>Total size of the block</returns>
			int GetSize(byte[] biffData, int offset);

			/// <summary>
			/// Returns the referenced data of the block (i.e. with meta data such as
			/// size stripped away).
			/// </summary>
			/// <param name="biffData">Entire BIFF data block</param>
			/// <param name="offset">Offset where block to analyize starts</param>
			/// <param name="blockSize">Previously parsed size of the entire block</param>
			/// <returns></returns>
			byte[] DeserializeData(byte[] biffData, int offset, int blockSize);

			/// <summary>
			/// Serializes back the data from whatever weird structure it was
			/// deserialized from before.
			/// </summary>
			/// <param name="data">Unserialized data</param>
			/// <returns>Serialized data, without tag info</returns>
			byte[] SerializeData(byte[] data);
		}

		/// <summary>
		/// A block starting with 4 bytes of size followed by data of that size. So 
		/// we have:
		/// 
		///    [4 bytes]    size
		///    [size bytes] data
		/// 
		/// </summary>
		public class SimpleSerializer : IBiffTagSerializer
		{
			public int GetSize(byte[] biffData, int offset)
			{
				// size of the block = 4 bytes indicating size + data size
				return 4 + BitConverter.ToInt32(biffData, offset);
			}

			public byte[] DeserializeData(byte[] blockData, int offset, int blockSize)
			{
				// what we need is the raw data, i.e. without the 4 size bytes
				var dataSize = blockSize - 4;

				// skip first 4 bytes that indicate size and take the rest
				return blockData.Skip(offset + 4).Take(dataSize).ToArray();
			}

			public byte[] SerializeData(byte[] data)
			{
				// concatenate block size and data
				return BitConverter.GetBytes(data.Length).Concat(data).ToArray();
			}
		}
	}
}
