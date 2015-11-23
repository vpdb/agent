using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using Akavache;
using Splat;
using VpdbAgent.Vpdb;

namespace VpdbAgent.Common.Extensions
{
	public static class BitmapImageMixin
	{

		private static readonly IVpdbClient VpdbClient = Locator.Current.GetService<IVpdbClient>();

		/// <summary>
		/// A combination of DownloadUrl and LoadImage, this method fetches an
		/// image from a remote URL (using the cached value if possible) and
		/// returns the image. 
		/// </summary>
		/// <param name="url">The URL to download.</param>
		/// <returns>A Future result representing the bitmap image. This
		/// Observable is guaranteed to be returned on the UI thread.</returns>
		public static IObservable<IBitmap> LoadImageFromVpdb(this IBlobCache This, string url, bool fetchAlways = false, float? desiredWidth = null, float? desiredHeight = null, DateTimeOffset? absoluteExpiration = null)
		{
			return This.DownloadUrl(VpdbClient.GetUri(url).AbsoluteUri, VpdbClient.GetAuthHeaders(), fetchAlways, absoluteExpiration)
				.SelectMany(ThrowOnBadImageBuffer)
				.SelectMany(x => BytesToImage(x, desiredWidth, desiredHeight));
		}

		/// <summary>
		/// Converts bad image buffers into an exception
		/// </summary>
		/// <returns>The byte[], or OnError if the buffer is corrupt (empty or 
		/// too small)</returns>
		public static IObservable<byte[]> ThrowOnBadImageBuffer(byte[] compressedImage)
		{
			return (compressedImage == null || compressedImage.Length < 64) ?
				Observable.Throw<byte[]>(new Exception("Invalid Image")) :
				Observable.Return(compressedImage);
		}

		static IObservable<IBitmap> BytesToImage(byte[] compressedImage, float? desiredWidth, float? desiredHeight)
		{
			return BitmapLoader.Current.Load(new MemoryStream(compressedImage), desiredWidth, desiredHeight).ToObservable();
		}
	}
}
