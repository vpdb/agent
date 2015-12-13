// Copyright (c) 2006, 2008 Tony Garnock-Jones <tonyg@lshift.net>
// Copyright (c) 2006, 2008 LShift Ltd. <query@lshift.net>
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation files
// (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software,
// and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS
// BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
// ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// Migration to C# (3.0 / .Net 2.0 for Visual Studio 2008) from 
// Javascript, Copyright (c) 2012 Tao Klerks <tao@klerks.biz>
// 
// This ported code is NOT cleaned up in terms of style, nor tested/optimized for 
// performance, nor even tested for correctness across all methods - it is an 
// extremely simplistic minimal-changes conversion/porting. The plan is to clean 
// it up to be more pleasant to look at an deal with at a later time.
// To anyone who is familiar with and understands the original terminology of 
// diff and diff3 concepts, I apologize for my fanciful naming strategy - I has 
// to come up with object names and haven't yet had a chance to review the 
// literature.
// Also added a "diff_merge_keepall()" implementation for simplistic 2-way merge.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SynchrotronNet
{
	/// <summary>
	/// Implementation of a three-way merge.
	/// </summary>
	/// <seealso cref="https://gist.github.com/TaoK/2633407"/>
	public class Diff
	{
		#region Arbitrarily-named in-between objects

		public class CandidateThing
		{
			public int File1Index { get; set; }
			public int File2Index { get; set; }
			public CandidateThing Chain { get; set; }
		}

		public class CommonOrDifferentThing
		{
			public List<string> Common { get; set; }
			public List<string> File1 { get; set; }
			public List<string> File2 { get; set; }
		}

		public class PatchDescriptionThing
		{
			internal PatchDescriptionThing() { }

			internal PatchDescriptionThing(string[] file, int offset, int length)
			{
				Offset = offset;
				Length = length;
				Chunk = new List<string>(file.SliceJs(offset, offset + length));
			}

			public int Offset { get; set; }
			public int Length { get; set; }
			public List<string> Chunk { get; set; }
		}

		public class PatchResult
		{
			public PatchDescriptionThing File1 { get; set; }
			public PatchDescriptionThing File2 { get; set; }
		}

		public class ChunkReference
		{
			public int Offset { get; set; }
			public int Length { get; set; }
		}

		public class DiffSet
		{
			public ChunkReference File1 { get; set; }
			public ChunkReference File2 { get; set; }
		}

		public enum Side
		{
			Conflict = -1,
			Left = 0,
			Old = 1,
			Right = 2
		}

		public class Diff3Set : IComparable<Diff3Set>
		{
			public Side Side { get; set; }
			public int File1Offset { get; set; }
			public int File1Length { get; set; }
			public int File2Offset { get; set; }
			public int File2Length { get; set; }

			public int CompareTo(Diff3Set other)
			{
				return File1Offset != other.File1Offset ? File1Offset.CompareTo(other.File1Offset) : Side.CompareTo(other.Side);
			}
		}

		public class Patch3Set
		{
			public Side Side { get; set; }
			public int Offset { get; set; }
			public int Length { get; set; }
			public int ConflictOldOffset { get; set; }
			public int ConflictOldLength { get; set; }
			public int ConflictRightOffset { get; set; }
			public int ConflictRightLength { get; set; }
		}

		private class ConflictRegion
		{
			public int File1RegionStart { get; set; }
			public int File1RegionEnd { get; set; }
			public int File2RegionStart { get; set; }
			public int File2RegionEnd { get; set; }
		}

		#endregion

		#region Merge Result Objects

		public interface IMergeResultBlock
		{
			// amusingly, I can't figure out anything they have in common.
		}

		public class MergeOkResultBlock : IMergeResultBlock
		{
			public string[] ContentLines { get; set; }
		}

		public class MergeConflictResultBlock : IMergeResultBlock
		{
			public string[] LeftLines { get; set; }
			public int LeftIndex { get; set; }
			public string[] OldLines { get; set; }
			public int OldIndex { get; set; }
			public string[] RightLines { get; set; }
			public int RightIndex { get; set; }
		}

		#endregion

		#region Methods

		#region Public API

		/// <summary>
		/// Text diff algorithm following Hunt and McIlroy 1976.
		/// J.W.Hunt and M.D.McIlroy, An algorithm for differential file
		/// comparison, Bell Telephone Laboratories CSTR #41 (1976)
		/// http://www.cs.dartmouth.edu/~doug/
		/// 
		/// Expects two arrays of strings.
		/// </summary>
		/// <param name="file1"></param>
		/// <param name="file2"></param>
		/// <returns></returns>
		public static CandidateThing LongestCommonSubsequence(string[] file1, string[] file2)
		{
			/* Text diff algorithm following Hunt and McIlroy 1976.
			 * J. W. Hunt and M. D. McIlroy, An algorithm for differential file
			 * comparison, Bell Telephone Laboratories CSTR #41 (1976)
			 * http://www.cs.dartmouth.edu/~doug/
			 *
			 * Expects two arrays of strings.
			 */
			var equivalenceClasses = new Dictionary<string, List<int>>();
			var candidates = new Dictionary<int, CandidateThing> {
				{ 0, new CandidateThing
					{
						File1Index = -1,
						File2Index = -1,
						Chain = null
					}
				}
			};

			for (var j = 0; j < file2.Length; j++) {
				var line = file2[j];
				if (equivalenceClasses.ContainsKey(line))
					equivalenceClasses[line].Add(j);
				else
					equivalenceClasses.Add(line, new List<int> { j });
			}

			for (var i = 0; i < file1.Length; i++) {
				var line = file1[i];
				var file2Indices = equivalenceClasses.ContainsKey(line) ? equivalenceClasses[line] : new List<int>();

				var r = 0;
				var c = candidates[0];

				foreach (var j in file2Indices)
				{
					int s;
					for (s = r; s < candidates.Count; s++) {
						if ((candidates[s].File2Index < j) && ((s == candidates.Count - 1) || (candidates[s + 1].File2Index > j))) {
							break;
						}
					}

					if (s < candidates.Count) {
						var newCandidate = new CandidateThing {
							File1Index = i,
							File2Index = j,
							Chain = candidates[s]
						};
						candidates[r] = c;
						r = s + 1;
						c = newCandidate;
						if (r == candidates.Count) {
							break; // no point in examining further (j)s
						}
					}
				}
				candidates[r] = c;
			}

			// At this point, we know the LCS: it's in the reverse of the
			// linked-list through .chain of
			// candidates[candidates.length - 1].

			return candidates[candidates.Count - 1];
		}

		/// <summary>
		/// We apply the LCS to build a "comm"-style picture of the
		/// differences between file1 and file2.
		/// </summary>
		/// <param name="file1"></param>
		/// <param name="file2"></param>
		/// <returns></returns>
		public static List<CommonOrDifferentThing> DiffComm(string[] file1, string[] file2)
		{
			var result = new List<CommonOrDifferentThing>();

			var tail1 = file1.Length;
			var tail2 = file2.Length;

			var common = new CommonOrDifferentThing {
				Common = new List<string>()
			};

			for (var candidate = LongestCommonSubsequence(file1, file2); candidate != null; candidate = candidate.Chain) {

				var different = new CommonOrDifferentThing {
					File1 = new List<string>(),
					File2 = new List<string>()
				};

				while (--tail1 > candidate.File1Index) {
					different.File1.Add(file1[tail1]);
				}

				while (--tail2 > candidate.File2Index) {
					different.File2.Add(file2[tail2]);
				}

				if (different.File1.Count > 0 || different.File2.Count > 0) {
					ProcessCommon(ref common, result);
					different.File1.Reverse();
					different.File2.Reverse();
					result.Add(different);
				}

				if (tail1 >= 0) {
					common.Common.Add(file1[tail1]);
				}
			}

			ProcessCommon(ref common, result);

			result.Reverse();
			return result;
		}

		/// <summary>
		/// We apply the LCD to build a JSON representation of a
		//// diff(1)-style patch.
		/// </summary>
		/// <param name="file1"></param>
		/// <param name="file2"></param>
		/// <returns></returns>
		public static List<PatchResult> DiffPatch(string[] file1, string[] file2)
		{
			var result = new List<PatchResult>();
			var tail1 = file1.Length;
			var tail2 = file2.Length;

			for (var candidate = LongestCommonSubsequence(file1, file2);
				 candidate != null;
				 candidate = candidate.Chain) {
				var mismatchLength1 = tail1 - candidate.File1Index - 1;
				var mismatchLength2 = tail2 - candidate.File2Index - 1;
				tail1 = candidate.File1Index;
				tail2 = candidate.File2Index;

				if (mismatchLength1 > 0 || mismatchLength2 > 0) {
					var thisResult = new PatchResult {
						File1 = new PatchDescriptionThing(file1, candidate.File1Index + 1, mismatchLength1),
						File2 = new PatchDescriptionThing(file2, candidate.File2Index + 1, mismatchLength2)
					};
					result.Add(thisResult);
				}
			}
			result.Reverse();
			return result;
		}

		/// <summary>
		/// Takes the output of <see cref="DiffPatch"/> and removes
		/// information from it. It can still be used by <see cref="Patch"/>,
		/// below, but can no longer be inverted.
		/// </summary>
		/// <param name="patch"></param>
		/// <returns></returns>
		public static List<PatchResult> StripPatch(List<PatchResult> patch)
		{
			return patch.Select(chunk => new PatchResult {
				File1 = new PatchDescriptionThing { Offset = chunk.File1.Offset, Length = chunk.File1.Length },
				File2 = new PatchDescriptionThing { Chunk = chunk.File1.Chunk }
			}).ToList();
		}

		/// <summary>
		/// Takes the output of Diff.diff_patch(), and inverts the
		/// sense of it, so that it can be applied to file2 to give
		/// file1 rather than the other way around.
		/// </summary>
		/// <param name="patch"></param>
		public static void InvertPatch(List<PatchResult> patch)
		{
			foreach (var chunk in patch) {
				var tmp = chunk.File1;
				chunk.File1 = chunk.File2;
				chunk.File2 = tmp;
			}
		}

		/// <summary>
		/// Applies a patch to a file.
		///
		/// Given file1 and file2, Diff.patch(file1, Diff.diff_patch(file1, file2)) should give file2.
		/// </summary>
		/// <param name="file"></param>
		/// <param name="patch"></param>
		/// <returns></returns>
		public static List<string> Patch(string[] file, List<PatchResult> patch)
		{
			var result = new List<string>();
			var commonOffset = 0;

			foreach (var chunk in patch) {
				CopyCommon(chunk.File1.Offset, ref commonOffset, file, result);
				result.AddRange(chunk.File2.Chunk);
				commonOffset += chunk.File1.Length;
			}

			CopyCommon(file.Length, ref commonOffset, file, result);
			return result;
		}

		/// <summary>
		/// Non-destructively merges two files.
		///
		/// This is NOT a three-way merge - content will often be DUPLICATED by this process, eg
		/// when starting from the same file some content was moved around on one of the copies.
		/// 
		/// To handle typical "common ancestor" situations and avoid incorrect duplication of 
		/// content, use diff3_merge instead.
		/// 
		/// This method's behaviour is similar to gnu diff's "if-then-else" (-D) format, but 
		/// without the if/then/else lines!
		/// </summary>
		/// <param name="file1"></param>
		/// <param name="file2"></param>
		/// <returns></returns>
		public static List<string> DiffMergeKeepall(string[] file1, string[] file2)
		{
			var result = new List<string>();
			var file1CompletedToOffset = 0;
			var diffPatches = DiffPatch(file1, file2);

			foreach (var chunk in diffPatches) {
				if (chunk.File2.Length > 0) {

					//copy any not-yet-copied portion of file1 to the end of this patch entry
					result.AddRange(file1.SliceJs(file1CompletedToOffset, chunk.File1.Offset + chunk.File1.Length));
					file1CompletedToOffset = chunk.File1.Offset + chunk.File1.Length;

					//copy the file2 portion of this patch entry
					result.AddRange(chunk.File2.Chunk);
				}
			}
			//copy any not-yet-copied portion of file1 to the end of the file
			result.AddRange(file1.SliceJs(file1CompletedToOffset, file1.Length));

			return result;
		}

		/// <summary>
		/// We apply the LCS to give a simple representation of the
		/// offsets and lengths of mismatched chunks in the input
		/// files. This is used by diff3_merge_indices below.
		/// </summary>
		/// <param name="file1"></param>
		/// <param name="file2"></param>
		/// <returns></returns>
		public static List<DiffSet> DiffIndices(string[] file1, string[] file2)
		{
			var result = new List<DiffSet>();
			var tail1 = file1.Length;
			var tail2 = file2.Length;

			for (var candidate = LongestCommonSubsequence(file1, file2);
				 candidate != null;
				 candidate = candidate.Chain) {
				var mismatchLength1 = tail1 - candidate.File1Index - 1;
				var mismatchLength2 = tail2 - candidate.File2Index - 1;
				tail1 = candidate.File1Index;
				tail2 = candidate.File2Index;

				if (mismatchLength1 > 0 || mismatchLength2 > 0) {
					result.Add(new DiffSet {
						File1 = new ChunkReference {
							Offset = tail1 + 1,
							Length = mismatchLength1
						},
						File2 = new ChunkReference {
							Offset = tail2 + 1,
							Length = mismatchLength2
						}
					});
				}
			}

			result.Reverse();
			return result;
		}

		/// <summary>
		/// Given three files, A, O, and B, where both A and B are
		/// independently derived from O, returns a fairly complicated
		/// internal representation of merge decisions it's taken. The
		/// interested reader may wish to consult
		///
		/// Sanjeev Khanna, Keshav Kunal, and Benjamin C. Pierce. "A
		/// Formal Investigation of Diff3." In Arvind and Prasad,
		/// editors, Foundations of Software Technology and Theoretical
		/// Computer Science (FSTTCS), December 2007.
		///
		/// (http://www.cis.upenn.edu/~bcpierce/papers/diff3-short.pdf)
		/// </summary>
		/// <param name="a">First file derived from <see cref="o"/></param>
		/// <param name="o">Base file</param>
		/// <param name="b">Second file derived from <see cref="o"/></param>
		/// <returns></returns>
		public static List<Patch3Set> Diff3MergeIndices(string[] a, string[] o, string[] b)
		{
			var m1 = DiffIndices(o, a);
			var m2 = DiffIndices(o, b);

			var hunks = new List<Diff3Set>();

			foreach (var index in m1) { AddHunk(index, Side.Left, hunks); }
			foreach (var index in m2) { AddHunk(index, Side.Right, hunks); }
			hunks.Sort();

			var result = new List<Patch3Set>();
			var commonOffset = 0;

			for (var hunkIndex = 0; hunkIndex < hunks.Count; hunkIndex++) {
				var firstHunkIndex = hunkIndex;
				var hunk = hunks[hunkIndex];
				var regionLhs = hunk.File1Offset;
				var regionRhs = regionLhs + hunk.File1Length;

				while (hunkIndex < hunks.Count - 1) {
					var maybeOverlapping = hunks[hunkIndex + 1];
					var maybeLhs = maybeOverlapping.File1Offset;
					if (maybeLhs > regionRhs)
						break;

					regionRhs = Math.Max(regionRhs, maybeLhs + maybeOverlapping.File1Length);
					hunkIndex++;
				}

				CopyCommon2(regionLhs, ref commonOffset, result);
				if (firstHunkIndex == hunkIndex) {
					// The "overlap" was only one hunk long, meaning that
					// there's no conflict here. Either a and o were the
					// same, or b and o were the same.
					if (hunk.File2Length > 0) {
						result.Add(new Patch3Set {
							Side = hunk.Side,
							Offset = hunk.File2Offset,
							Length = hunk.File2Length
						});
					}
				} else {
					// A proper conflict. Determine the extents of the
					// regions involved from a, o and b. Effectively merge
					// all the hunks on the left into one giant hunk, and
					// do the same for the right; then, correct for skew
					// in the regions of o that each side changed, and
					// report appropriate spans for the three sides.

					var regions = new Dictionary<Side, ConflictRegion>
						{
							{
								Side.Left,
								new ConflictRegion
									{
										File1RegionStart = a.Length,
										File1RegionEnd = -1,
										File2RegionStart = o.Length,
										File2RegionEnd = -1
									}
							},
							{
								Side.Right,
								new ConflictRegion
									{
										File1RegionStart = b.Length,
										File1RegionEnd = -1,
										File2RegionStart = o.Length,
										File2RegionEnd = -1
									}
							}
						};

					for (var i = firstHunkIndex; i <= hunkIndex; i++) {
						hunk = hunks[i];
						var side = hunk.Side;
						var r = regions[side];
						var oLhs = hunk.File1Offset;
						var oRhs = oLhs + hunk.File1Length;
						var abLhs = hunk.File2Offset;
						var abRhs = abLhs + hunk.File2Length;
						r.File1RegionStart = Math.Min(abLhs, r.File1RegionStart);
						r.File1RegionEnd = Math.Max(abRhs, r.File1RegionEnd);
						r.File2RegionStart = Math.Min(oLhs, r.File2RegionStart);
						r.File2RegionEnd = Math.Max(oRhs, r.File2RegionEnd);
					}
					var aLhs = regions[Side.Left].File1RegionStart + (regionLhs - regions[Side.Left].File2RegionStart);
					var aRhs = regions[Side.Left].File1RegionEnd + (regionRhs - regions[Side.Left].File2RegionEnd);
					var bLhs = regions[Side.Right].File1RegionStart + (regionLhs - regions[Side.Right].File2RegionStart);
					var bRhs = regions[Side.Right].File1RegionEnd + (regionRhs - regions[Side.Right].File2RegionEnd);

					result.Add(new Patch3Set {
						Side = Side.Conflict,
						Offset = aLhs,
						Length = aRhs - aLhs,
						ConflictOldOffset = regionLhs,
						ConflictOldLength = regionRhs - regionLhs,
						ConflictRightOffset = bLhs,
						ConflictRightLength = bRhs - bLhs
					});
				}

				commonOffset = regionRhs;
			}

			CopyCommon2(o.Length, ref commonOffset, result);
			return result;
		}

		/// <summary>
		/// Applies the output of <see cref="Diff3MergeIndices"/> to actually
		/// construct the merged file; the returned result alternates
		/// between "ok" and "conflict" blocks.
		/// </summary>
		/// 
		/// <param name="a">First file derived from <see cref="o"/></param>
		/// <param name="o">Base file</param>
		/// <param name="b">Second file derived from <see cref="o"/></param>
		/// <param name="excludeFalseConflicts"></param>
		/// <returns></returns>
		public static List<IMergeResultBlock> Diff3Merge(string[] a, string[] o, string[] b, bool excludeFalseConflicts)
		{
			var result = new List<IMergeResultBlock>();
			var files = new Dictionary<Side, string[]> {
				{ Side.Left, a },
				{ Side.Old, o },
				{ Side.Right, b }
			};
			var indices = Diff3MergeIndices(a, o, b);
			var okLines = new List<string>();

			foreach (var x in indices) {
				var side = x.Side;
				if (side == Side.Conflict) {
					if (excludeFalseConflicts && !IsTrueConflict(x, a, b)) {
						okLines.AddRange(files[0].SliceJs(x.Offset, x.Offset + x.Length));
					} else {
						FlushOk(okLines, result);
						result.Add(new MergeConflictResultBlock {
							LeftLines = a.SliceJs(x.Offset, x.Offset + x.Length),
							LeftIndex = x.Offset,
							OldLines = o.SliceJs(x.ConflictOldOffset, x.ConflictOldOffset + x.ConflictOldLength),
							OldIndex = x.ConflictOldOffset,
							RightLines = b.SliceJs(x.ConflictRightOffset, x.ConflictRightOffset + x.ConflictRightLength),
							RightIndex = x.Offset
						});
					}
				} else {
					okLines.AddRange(files[side].SliceJs(x.Offset, x.Offset + x.Length));
				}
			}

			FlushOk(okLines, result);
			return result;
		}

		#endregion Public API

		#region Private Helpers

		private static void ProcessCommon(ref CommonOrDifferentThing common, ICollection<CommonOrDifferentThing> result)
		{
			if (common.Common.Count > 0) {
				common.Common.Reverse();
				result.Add(common);
				common = new CommonOrDifferentThing();
			}
		}

		private static void CopyCommon(int targetOffset, ref int commonOffset, IReadOnlyList<string> file, ICollection<string> result)
		{
			while (commonOffset < targetOffset) {
				result.Add(file[commonOffset]);
				commonOffset++;
			}
		}


		private static void AddHunk(DiffSet h, Side side, List<Diff3Set> hunks)
		{
			hunks.Add(new Diff3Set {
				Side = side,
				File1Offset = h.File1.Offset,
				File1Length = h.File1.Length,
				File2Offset = h.File2.Offset,
				File2Length = h.File2.Length
			});
		}

		private static void CopyCommon2(int targetOffset, ref int commonOffset, ICollection<Patch3Set> result)
		{
			if (targetOffset > commonOffset) {
				result.Add(new Patch3Set {
					Side = Side.Old,
					Offset = commonOffset,
					Length = targetOffset - commonOffset
				});
			}
		}

		private static void FlushOk(List<string> okLines, ICollection<IMergeResultBlock> result)
		{
			if (okLines.Count > 0) {
				var okResult = new MergeOkResultBlock {ContentLines = okLines.ToArray()};
				result.Add(okResult);
			}
			okLines.Clear();
		}

		private static bool IsTrueConflict(Patch3Set rec, IReadOnlyList<string> a, IReadOnlyList<string> b)
		{
			if (rec.Length != rec.ConflictRightLength)
				return true;

			var aoff = rec.Offset;
			var boff = rec.ConflictRightOffset;

			for (var j = 0; j < rec.Length; j++) {
				if (a[j + aoff] != b[j + boff])
					return true;
			}
			return false;
		}

		#endregion Private Helpers

		#endregion
	}

	#region Extra JS-emulating stuff

	public static class ArrayExtension
	{
		public static T[] SliceJs<T>(this T[] array, int startingIndex, int followingIndex)
		{
			if (followingIndex > array.Length)
				followingIndex = array.Length;

			var outArray = new T[followingIndex - startingIndex];

			for (var i = 0; i < outArray.Length; i++)
				outArray[i] = array[i + startingIndex];

			return outArray;
		}
	}

	#endregion
}

#region Extension Method Support - remove when including in other projects that already have it.

namespace System.Runtime.CompilerServices
{
	[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class
		 | AttributeTargets.Method)]
	public sealed class ExtensionAttribute : Attribute { }
}

#endregion