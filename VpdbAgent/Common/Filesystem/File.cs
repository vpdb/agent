using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Common.Filesystem
{

	/// <summary>
	/// A non-static interface wrapped around <see cref="System.IO.File"/> 
	/// operations that can be switched out in unit tests.
	/// </summary>
	public interface IFile
	{
		/// <summary>
		/// Determines whether the specified file exists.
		/// </summary>
		/// 
		/// <returns>
		/// true if the caller has the required permissions and <paramref name="path"/> contains the name of an existing file; otherwise, false. This method also returns false if <paramref name="path"/> is null, an invalid path, or a zero-length string. If the caller does not have sufficient permissions to read the specified file, no exception is thrown and the method returns false regardless of the existence of <paramref name="path"/>.
		/// </returns>
		/// <param name="path">The file to check. </param><filterpriority>1</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
		[SecuritySafeCritical]
		bool Exists(string path);

		/// <summary>
		/// Opens a text file, reads all lines of the file, and then closes the file.
		/// </summary>
		/// 
		/// <returns>
		/// A string containing all lines of the file.
		/// </returns>
		/// <param name="path">The file to open for reading. </param><exception cref="T:System.ArgumentException"><paramref name="path"/> is a zero-length string, contains only white space, or contains one or more invalid characters as defined by <see cref="F:System.IO.Path.InvalidPathChars"/>. </exception><exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null. </exception><exception cref="T:System.IO.PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length. For example, on Windows-based platforms, paths must be less than 248 characters, and file names must be less than 260 characters. </exception><exception cref="T:System.IO.DirectoryNotFoundException">The specified path is invalid (for example, it is on an unmapped drive). </exception><exception cref="T:System.IO.IOException">An I/O error occurred while opening the file. </exception><exception cref="T:System.UnauthorizedAccessException"><paramref name="path"/> specified a file that is read-only.-or- This operation is not supported on the current platform.-or- <paramref name="path"/> specified a directory.-or- The caller does not have the required permission. </exception><exception cref="T:System.IO.FileNotFoundException">The file specified in <paramref name="path"/> was not found. </exception><exception cref="T:System.NotSupportedException"><paramref name="path"/> is in an invalid format. </exception><exception cref="T:System.Security.SecurityException">The caller does not have the required permission. </exception><filterpriority>1</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
		[SecuritySafeCritical]
		string ReadAllText(string path);

		/// <summary>
		/// Creates a new file, writes the specified string to the file, and then closes the file. If the target file already exists, it is overwritten.
		/// </summary>
		/// <param name="path">The file to write to. </param><param name="contents">The string to write to the file. </param><exception cref="T:System.ArgumentException"><paramref name="path"/> is a zero-length string, contains only white space, or contains one or more invalid characters as defined by <see cref="F:System.IO.Path.InvalidPathChars"/>. </exception><exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null or <paramref name="contents"/> is empty.  </exception><exception cref="T:System.IO.PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length. For example, on Windows-based platforms, paths must be less than 248 characters, and file names must be less than 260 characters. </exception><exception cref="T:System.IO.DirectoryNotFoundException">The specified path is invalid (for example, it is on an unmapped drive). </exception><exception cref="T:System.IO.IOException">An I/O error occurred while opening the file. </exception><exception cref="T:System.UnauthorizedAccessException"><paramref name="path"/> specified a file that is read-only.-or- This operation is not supported on the current platform.-or- <paramref name="path"/> specified a directory.-or- The caller does not have the required permission. </exception><exception cref="T:System.NotSupportedException"><paramref name="path"/> is in an invalid format. </exception><exception cref="T:System.Security.SecurityException">The caller does not have the required permission. </exception><filterpriority>1</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
		[SecuritySafeCritical]
		void WriteAllText(string path, string contents);
	}

	public class File : IFile
	{
		public bool Exists(string path)
		{
			return System.IO.File.Exists(path);
		}

		public string ReadAllText(string path)
		{
			return System.IO.File.ReadAllText(path);
		}

		public void WriteAllText(string path, string contents)
		{
			System.IO.File.WriteAllText(path, contents);
		}
	}
}
