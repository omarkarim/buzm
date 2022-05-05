using System;
using System.IO;
using Buzm.Utility.Algorithms;

namespace Buzm.Network.Files
{
	public class FilePiece : IHashable
	{		
		private int m_Size;
		private int m_Index;

		private byte[] m_Hash;
		private byte[] m_Bytes;

		public FilePiece( int index, int size )
		{			
			m_Index = index;
			m_Size = size;
		}

		public void Read( long position, FileStream stream )
		{
			Seek( position, stream ); // set pointer
			m_Bytes = new byte[m_Size]; // init memory			
			
			int count = stream.Read( m_Bytes, 0, m_Size );
			if( count != m_Size ) throw new IOException( "Miscount bytes read" );
		}

		public void Write( long position, FileStream stream, bool release )
		{			
			Seek( position, stream ); // set pointer
			stream.Write( m_Bytes, 0, m_Size );

			stream.Flush(); // commit stream to disk			
			if( release ) m_Bytes = null; // clear memory
		}

		public void Seek( long position, FileStream stream )
		{			
			if( position != stream.Position )
			{
				// only seek position if necessary
				stream.Seek( position, SeekOrigin.Begin );
			}
		}

		public byte[] Bytes
		{
			get { return m_Bytes; }
			set { m_Bytes = value; }
		}

		public byte[] Hash
		{
			get { return m_Hash; }
			set { m_Hash = value; }
		}

		public int Size
		{
			get { return m_Size; }
			set { m_Size = value; }
		}
	}
}
