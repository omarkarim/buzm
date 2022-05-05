using System;
using System.IO;
using System.Diagnostics;
using Buzm.Utility.Algorithms;
using NUnit.Framework;
using Buzm.Utility;

namespace Buzm.Network.Files
{
	public class FileModel
	{				
		private string m_Guid;		
		private string m_Path;

		private int m_PieceSize;
		private int m_LastPiece;		
		private int m_PieceCount;
				
		private long m_FileSize;		
		private long m_BytesLeft;

		private HashTree m_HashTree;
		private FileStream m_Stream;
		private FilePiece[] m_Pieces;

		private const int KBYTE = 1024;
		private const int PIECE_SIZE = 32 * KBYTE;

		public FileModel( string guid, string path ) : this( guid, path, PIECE_SIZE ) { }
		public FileModel( string guid, string path, int pieceSize )
		{
			m_Guid = guid;
			m_Path = path;		
			
			m_PieceSize = pieceSize;
			m_HashTree = new HashTree();

			//TODO: check for zero-length file
			//TODO: check for file and errors
			//TODO: check for read-only flag on file
			m_Stream = new FileStream( path, FileMode.OpenOrCreate, 
				FileAccess.ReadWrite, FileShare.Read );
			
			//TODO: call dispose on filestream when done
		}

		public void ImportFile( string path )
		{
			//TODO: wrap in try/catch/retry
			FileInfo inFile = new FileInfo( path );
			if( inFile.Exists )
			{
				m_BytesLeft = FileSize = inFile.Length;
				DateTime writeTime = inFile.LastWriteTime;				
				
				FileStream inStream = inFile.Open( FileMode.Open,
					FileAccess.Read, FileShare.ReadWrite );

				int size = m_PieceSize; // set default 				
				m_Pieces = new FilePiece[m_PieceCount];

				for( int idx = 0; idx < m_PieceCount; idx++ )
				{
					long position = m_FileSize - m_BytesLeft;
					if( m_BytesLeft < size ) size = (int)m_BytesLeft;
					
					FilePiece piece = new FilePiece( idx, size );
					m_Pieces[idx] = piece;
				
					piece.Read( position, inStream );
					piece.Hash = m_HashTree.GetLeafHash( piece.Bytes );
					piece.Write( position, m_Stream, true );
					
					m_BytesLeft -= size;
					//TODO: raise progress event
				}

				//TODO: Handle zero length file
				//TODO: Test file size larger than int
				//TODO: compare length and writetime again
				inStream.Close(); //TODO: in finally from HiveModel
				inFile.Refresh();

				m_HashTree.BuildTree( m_Pieces );
			}
		}

		public long FileSize
		{
			get { return m_FileSize; }
			set // file size and related values
			{ 
				m_FileSize = value; // update file size
				m_PieceCount = (int)( m_FileSize / m_PieceSize );

				m_LastPiece = (int)( m_FileSize % m_PieceSize );
				if( m_LastPiece > 0 ) m_PieceCount++;
			}
		}

		public string Path
		{
			get { return m_Path; }
			set { m_Path = value; }
		}

		public string Guid
		{
			get { return m_Guid; }
			set { m_Guid = value; }
		}

		public void Close()
		{
			m_Stream.Close();
		}

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class FileModelTest
		{
			private string m_Guid;
			private string m_SrcFolder;
			private string m_DstFolder;			

			private Random m_Randomizer;
			private ConsoleListener m_Listener;
			
			[SetUp] public void SetUp()
			{
				// bind to NUnit console listener
				m_Listener = new ConsoleListener();
				Trace.Listeners.Add( m_Listener );
				Log.TraceLevel = TraceLevel.Info;

				m_Randomizer = new Random();
				m_Guid = System.Guid.NewGuid().ToString();

				m_SrcFolder = FileUtils.CreateTempFolder();
				m_DstFolder = FileUtils.CreateTempFolder();
			}

			[TearDown] public void TearDown()
			{
				Directory.Delete( m_SrcFolder, true );
				Directory.Delete( m_DstFolder, true );

				// remove NUnit console listener
				Trace.Listeners.Remove( m_Listener );				
			}

			[Test] public void ImportFileTest()
			{				
				DateTime startTime = DateTime.Now;
				CreateRandomFile( m_SrcFolder + "1GBFile", KBYTE * KBYTE * KBYTE );
				
				TimeSpan duration = DateTime.Now - startTime;
				Log.Write( TraceLevel.Info, "1GB file created in: " + duration.ToString(), "FileModelTest.ImportFileTest" );
				
				// use file model to import source file to destination
				FileModel dstFile = new FileModel( m_Guid, m_DstFolder + "1GBFile" );			
				
				startTime = DateTime.Now;
				//dstFile.ImportFile( "C:\\Invoice Template.doc" );
				dstFile.ImportFile( m_SrcFolder + "1GBFile" );
				dstFile.Close();

				duration = DateTime.Now - startTime;
				Log.Write( TraceLevel.Info, "1GB file imported in: " + duration.ToString(), "FileModelTest.ImportFileTest" );
			}

			private void CreateRandomFile( string path, long size )
			{				
				FileStream fs = new FileStream( path, FileMode.CreateNew );

				for( long i = 0; i < size; i += PIECE_SIZE )
				{
					byte[] rndBytes = new byte[PIECE_SIZE];
					m_Randomizer.NextBytes( rndBytes );
					fs.Write( rndBytes, 0, PIECE_SIZE );					
				}

				fs.SetLength( size );
				fs.Close();
			}
		}

		#endif
		#endregion
	}
}
