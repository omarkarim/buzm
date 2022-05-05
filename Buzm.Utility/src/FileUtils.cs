using System;
using System.IO;
using System.Diagnostics;
using NUnit.Framework;

namespace Buzm.Utility
{
	/// <summary>File and directory functions not
	/// provided within the .NET Framework </summary>
	public class FileUtils
	{
		/// <summary> Recursively copies all files and folders
		/// in the source folder to the destination folder </summary>
		/// <param name="src">Absolute path to the source folder</param>
		/// <param name="dst">Absolute path to the destination folder</param>
		public static void CopyDirectory( string src, string dst )
		{
			dst = AppendSeparator( dst ); // append trailing separator
			// create destination directory if it doesn't already exist
			if( !Directory.Exists( dst ) ) Directory.CreateDirectory( dst );
			
			// extract all files and directories inside the source
			string[] files = Directory.GetFileSystemEntries( src );
			
			string destPath; // copy dest
			foreach( string item in files )
			{
				// construct destination path from item
				destPath = dst + Path.GetFileName( item );
				
				// if the item is a sub-directory then recurse to copy tree
				if( Directory.Exists( item ) ) CopyDirectory( item, destPath );					
				else 
				{	// if dest file doesn't exist or it exists & is writable
					if( !File.Exists( destPath ) || !IsReadOnly( destPath ) ) 
						 File.Copy( item, destPath, true ); // copy the file
				}
			}
		}

		/// <summary>Returns true if the ReadOnly
		/// attribute is set on this file, otherwise
		/// returns false. The file must exist or the
		/// method should throw an exception </summary>
		public static bool IsReadOnly( string file )
		{
			if( ( File.GetAttributes( file ) & FileAttributes.ReadOnly ) == 
				  FileAttributes.ReadOnly ) return true;
			else return false;
		}

		/// <summary>Appends a trailing separator 
		///  to the path if one is missing</summary>
		public static string AppendSeparator( string folder )
		{
			char separator = Path.DirectorySeparatorChar; // system specific
			if( (folder == null) || (folder.Length == 0) ) return String.Empty;

			if( folder[folder.Length - 1] != separator ) return folder += separator;
			else return folder; // the folder already ends with a separator
		}

		/// <summary>Creates folder if needed </summary>
		public static string CreateFolder( string folder )
		{
			if( !Directory.Exists( folder ) ) Directory.CreateDirectory( folder );
			return folder; // return created or existing folder
		}

		/// <summary>Creates a uniquely named folder
		/// in the machine's temp location </summary>
		public static string CreateTempFolder( )
		{
			string guid = Guid.NewGuid().ToString();
			string path = AppendSeparator( Path.GetTempPath() );
			string folder = AppendSeparator( path + "Buzm_" + guid );
			return CreateFolder( folder ); // return new temp folder
		}

		/// <summary>Creates empty read-only hidden
		/// file at the specified location</summary>		
		public static bool TouchMarkerFile( string path )
		{
			FileStream fs = null; // init for finally
			try // creating file and setting attributes
			{
				FileInfo file = new FileInfo( path );
				if( !file.Exists ) // avoid overwrite
				{
					fs = file.Create(); // touch file
					file.Refresh(); // reload attributes

					file.Attributes = ( ( file.Attributes
						| FileAttributes.ReadOnly )
						| FileAttributes.Hidden );
				}				
				return true;
			}
			catch( Exception e )
			{
				Log.Write( "Could not touch file: " + path,
				TraceLevel.Error, "FileUtils.TouchMarkerFile", e );
				return false; // file may not be created
			}
			finally
			{
				// ensure marker file is closed regardless
				if( fs != null ) { try { fs.Close(); } catch {} }
			}			
		}

		/// <summary>Returns friendly file or folder 
		/// name based on the specified guid</summary>
		public static string GuidToFileName( string guid )
		{
			return "{" + guid + "}"; // based on MS standard
		}
	}

	#region NUnit Automated Test Cases

	[TestFixture] public class FileUtilsTest
	{	
		[SetUp] public void SetUp() { }
		[TearDown] public void TearDown(){ }

		[Test] public void IsReadOnlyTest()
		{
			string m_TempFile = Path.GetTempFileName();
			bool readOnly = FileUtils.IsReadOnly( m_TempFile );
			Assertion.Assert( "File should not be read only.", !readOnly );

			// make file read only
			File.SetAttributes( m_TempFile, FileAttributes.ReadOnly );
			readOnly = FileUtils.IsReadOnly( m_TempFile ); // check again
			Assertion.Assert( "File should be read only after set", readOnly );

			// cleanup temp file
			File.SetAttributes( m_TempFile, 0 );
			File.Delete( m_TempFile );
		}

		[Test, ExpectedException(typeof(FileNotFoundException))]
		public void FileMissingReadOnlyTest()
		{
			// attempt to check read only attribute on unknown file
			bool readOnly = FileUtils.IsReadOnly( @"c:\unknown_file" );
		}

		[Test] public void CopyDirectoryTest()
		{
			string tempFolder = Path.GetTempPath();
			string srcFolder = tempFolder + @"\Source\";
			string subSrcFolder = srcFolder + @"\subfld\";

			// create source folders and files
			Directory.CreateDirectory( subSrcFolder );
			FileStream fs1 = File.Create( srcFolder + "sourceFolder.txt" );
			FileStream fs2 = File.Create( subSrcFolder + "subFolder.txt" );
			fs1.Close(); fs2.Close();

			// copy source folder to destination
			string destFolder = tempFolder + @"\Destination\";
			FileUtils.CopyDirectory( srcFolder, destFolder );

			// check inner most file existence
			bool exists = File.Exists( destFolder + @"subfld\subFolder.txt" );
			Assertion.Assert( "Copied file not found", exists );

			// Cleanup temp folders
			Directory.Delete( srcFolder, true );
			Directory.Delete( destFolder, true );
		}

		[Test] public void AppendSeparatorTest()
		{
			string inSepFolder = @"C:\Temp\";
			string inNoSepFolder = @"C:\Temp";
			
			string outSepFolder = FileUtils.AppendSeparator( inSepFolder );
			string outNoSepFolder = FileUtils.AppendSeparator( inNoSepFolder );

			Assert.AreEqual( inSepFolder, outSepFolder, "Extra separator added" );
			Assert.AreEqual( inNoSepFolder + Path.DirectorySeparatorChar, outNoSepFolder, "Extra separator not added" );

			Assert.IsEmpty( FileUtils.AppendSeparator( String.Empty ), "Expected empty path for empty folder" );
			Assert.IsEmpty( FileUtils.AppendSeparator( null ), "Expected empty path for null folder" );
		}

		[Test] public void CreateFolderTest()
		{
			string tempFolder = Path.GetTempPath();
			string inFolder = tempFolder + @"\folder";
			string outFolder = FileUtils.CreateFolder( inFolder );

			// check inner most file existence
			bool exists = Directory.Exists( outFolder );
			Assertion.Assert( "New folder not found", exists );
			Directory.Delete( outFolder, true );
		}

		[Test] public void CreateTempFolderTest()
		{
			string tempFolder = FileUtils.CreateTempFolder();
			string tempFile = tempFolder + "test.txt";
			FileStream fs = File.Create( tempFile );
			fs.Close(); // close file for delete

			// check inner temp file existence
			bool exists = File.Exists( tempFile );
			Assertion.Assert( "Temp file not found", exists );
			Directory.Delete( tempFolder, true ); // cleanup
		}

		[Test] public void TouchMarkerFileTest()
		{
			string tempFolder = FileUtils.CreateTempFolder();
			string tempFile = tempFolder + "marker.dat";
						
			Assert.IsTrue( FileUtils.TouchMarkerFile( tempFile ), "Touch failed" );
			Assert.IsTrue( File.Exists( tempFile ), "File doesn't exist after touch" );

			FileAttributes attributes = File.GetAttributes( tempFile ); // get marker attributes
			Assert.AreEqual( attributes & FileAttributes.Hidden, FileAttributes.Hidden, "Missing hidden attribute" );
			Assert.AreEqual( attributes & FileAttributes.ReadOnly, FileAttributes.ReadOnly, "Missing read-only attribute" );

			Assert.IsTrue( FileUtils.TouchMarkerFile( tempFile ), "Retouch failed" );
			Assert.IsTrue( File.Exists( tempFile ), "File doesn't exist after retouch" );

			File.SetAttributes( tempFile, 0 ); // clear read-only attribute
			Directory.Delete( tempFolder, true ); // cleanup test data
		}
	}

	#endregion
}
