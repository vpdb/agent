using System.Diagnostics.CodeAnalysis;
using System.Security;

namespace VpdbAgent.Common.Filesystem
{
	/// <summary>
	/// A non-static interface wrapped around <see cref="System.IO.Directory"/> 
	/// operations that can be switched out in unit tests.
	/// </summary>
	public interface IDirectory
	{
		/// <summary>
		/// Determines whether the given path refers to an existing directory on disk.
		/// </summary>
		/// 
		/// <returns>
		/// true if <paramref name="path"/> refers to an existing directory; false if the directory does not exist or an error occurs when trying to determine if the specified file exists.
		/// </returns>
		/// <param name="path">The path to test. </param><filterpriority>1</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
		[SecuritySafeCritical]
		bool Exists(string path);

		/// <summary>
		/// Returns the names of files (including their paths) in the specified directory.
		/// </summary>
		/// 
		/// <returns>
		/// An array of the full names (including paths) for the files in the specified directory, or an empty array if no files are found.
		/// </returns>
		/// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param><exception cref="T:System.IO.IOException"><paramref name="path"/> is a file name.-or-A network error has occurred. </exception><exception cref="T:System.UnauthorizedAccessException">The caller does not have the required permission. </exception><exception cref="T:System.ArgumentException"><paramref name="path"/> is a zero-length string, contains only white space, or contains one or more invalid characters. You can query for invalid characters by using the <see cref="M:System.IO.Path.GetInvalidPathChars"/> method. </exception><exception cref="T:System.ArgumentNullException"><paramref name="path"/> is null. </exception><exception cref="T:System.IO.PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length. For example, on Windows-based platforms, paths must be less than 248 characters and file names must be less than 260 characters. </exception><exception cref="T:System.IO.DirectoryNotFoundException">The specified path is not found or is invalid (for example, it is on an unmapped drive). </exception><filterpriority>1</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
		string[] GetFiles(string path);
	}

	[ExcludeFromCodeCoverage]
	public class Directory : IDirectory
	{
		public bool Exists(string path)
		{
			return System.IO.Directory.Exists(path);
		}

		public string[] GetFiles(string path)
		{
			return System.IO.Directory.GetFiles(path);
		}
	}
}
