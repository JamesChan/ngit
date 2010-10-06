using ICSharpCode.SharpZipLib.Zip.Compression;
using NGit.Storage.File;
using Sharpen;

namespace NGit.Storage.File
{
	/// <summary>A window of data currently stored within a cache.</summary>
	/// <remarks>
	/// A window of data currently stored within a cache.
	/// <p>
	/// All bytes in the window can be assumed to be "immediately available", that is
	/// they are very likely already in memory, unless the operating system's memory
	/// is very low and has paged part of this process out to disk. Therefore copying
	/// bytes from a window is very inexpensive.
	/// </p>
	/// </remarks>
	internal abstract class ByteWindow
	{
		protected internal readonly PackFile pack;

		protected internal readonly long start;

		protected internal readonly long end;

		protected internal ByteWindow(PackFile p, long s, int n)
		{
			pack = p;
			start = s;
			end = start + n;
		}

		internal int Size()
		{
			return (int)(end - start);
		}

		internal bool Contains(PackFile neededFile, long neededPos)
		{
			return pack == neededFile && start <= neededPos && neededPos < end;
		}

		/// <summary>Copy bytes from the window to a caller supplied buffer.</summary>
		/// <remarks>Copy bytes from the window to a caller supplied buffer.</remarks>
		/// <param name="pos">offset within the file to start copying from.</param>
		/// <param name="dstbuf">destination buffer to copy into.</param>
		/// <param name="dstoff">offset within <code>dstbuf</code> to start copying into.</param>
		/// <param name="cnt">
		/// number of bytes to copy. This value may exceed the number of
		/// bytes remaining in the window starting at offset
		/// <code>pos</code>.
		/// </param>
		/// <returns>
		/// number of bytes actually copied; this may be less than
		/// <code>cnt</code> if <code>cnt</code> exceeded the number of
		/// bytes available.
		/// </returns>
		internal int Copy(long pos, byte[] dstbuf, int dstoff, int cnt)
		{
			return Copy((int)(pos - start), dstbuf, dstoff, cnt);
		}

		/// <summary>Copy bytes from the window to a caller supplied buffer.</summary>
		/// <remarks>Copy bytes from the window to a caller supplied buffer.</remarks>
		/// <param name="pos">offset within the window to start copying from.</param>
		/// <param name="dstbuf">destination buffer to copy into.</param>
		/// <param name="dstoff">offset within <code>dstbuf</code> to start copying into.</param>
		/// <param name="cnt">
		/// number of bytes to copy. This value may exceed the number of
		/// bytes remaining in the window starting at offset
		/// <code>pos</code>.
		/// </param>
		/// <returns>
		/// number of bytes actually copied; this may be less than
		/// <code>cnt</code> if <code>cnt</code> exceeded the number of
		/// bytes available.
		/// </returns>
		protected internal abstract int Copy(int pos, byte[] dstbuf, int dstoff, int cnt);

		/// <exception cref="Sharpen.DataFormatException"></exception>
		internal int SetInput(long pos, Inflater inf)
		{
			return SetInput((int)(pos - start), inf);
		}

		/// <exception cref="Sharpen.DataFormatException"></exception>
		protected internal abstract int SetInput(int pos, Inflater inf);
	}
}