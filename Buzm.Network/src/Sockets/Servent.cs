using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Collections;
using System.Diagnostics;
using Buzm.Utility;

namespace Buzm.Network.Sockets
{

	public interface IServentFactory
	{
		void CreateServent( Socket socket );
		void CreateServent( string host, int port );
	}

	public delegate void DataReceivedEventHandler( object data, Servent svr );
	public delegate void ConnectionClosedEventHandler( Servent svr );

	public class Servent : TcpClient
	{
		private int		m_ReadState;
		private int		m_DataLength;
		private byte[]	m_ClientBuffer;	

		private ServentRole m_Role;
		private string m_ServentGuid;
		private MemoryStream  m_BufferStream;
		private NetworkStream m_ClientStream;
		private PeerEndPoint  m_PeerEndPoint;

		public event DataReceivedEventHandler DataReceived;
		public event ConnectionClosedEventHandler ConnectionClosed;
		private volatile ServentStatus m_Status = ServentStatus.Connecting;

		public Servent( PeerEndPoint endPoint ) : base( endPoint.Host, endPoint.Port )
		{				
			m_PeerEndPoint = endPoint;
			m_Role = ServentRole.Client;
			m_ServentGuid  = Guid.NewGuid().ToString();
		}
		
		public Servent( Socket socket ) : base()
		{
			m_PeerEndPoint = new PeerEndPoint( ((IPEndPoint)socket.RemoteEndPoint).Address.ToString(), 
											   ((IPEndPoint)socket.RemoteEndPoint).Port );
			base.Client = socket;
			m_Role = ServentRole.Server;
			m_ServentGuid = Guid.NewGuid().ToString();			
		}

		public void BeginReceive( )
		{	
			// TODO: Increase buffer size
			// base.SendBufferSize = 32768;
			// base.ReceiveBufferSize = 32768;
			m_ClientStream = base.GetStream();
			m_ClientBuffer = new byte[base.ReceiveBufferSize * 2]; // 2x to allow overflow loop
			base.Client.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1 );
			m_ClientStream.BeginRead( m_ClientBuffer, 0, base.ReceiveBufferSize - 1, new AsyncCallback(OnReceive), null );	
		}

		public void Send( object data )
		{
			Send( data, true );
		}

		public void Send( object data, bool async )
		{	
			if( async ) ThreadPool.QueueUserWorkItem( new WaitCallback( this.WriteToStream ), data );
			else WriteToStream( data );
		}

		private void WriteToStream( object data )
		{
			try // writing generic object to the stream
			{
				MemoryStream buffer = new MemoryStream();
				Serialization.WriteObject( data, buffer );								  
				
				lock( m_ClientStream ) // For multiple threadpool threads
				{	
					// Write in-memory object buffer to the network stream
					if( m_ClientStream.CanWrite ) buffer.WriteTo( m_ClientStream ); 
					else Log.Write( TraceLevel.Warning, "Client stream is not writable.", "Servent.WriteToStream" );
				}
			}
			catch( Exception e ){ Log.Write( TraceLevel.Warning, "Failed write to client stream: " + e.ToString(), "Servent.WriteToStream" ); }
		}

		private void OnReceive( IAsyncResult ar )
		{
			int count;
			try { count = base.GetStream().EndRead( ar ); }
			catch { count = 0; }

			if( count > 0 )
			{
				try { ReadFromStream( count ); } //TODO: finally code might throw exception if socket is closed
				catch( Exception e ){ Log.Write( TraceLevel.Warning, "Failed to read stream: " + e.ToString(), "Servent.OnReceive" ); }
				finally { m_ClientStream.BeginRead( m_ClientBuffer, 0, base.ReceiveBufferSize - 1, new AsyncCallback(OnReceive), null ); }
			}
			else OnConnectionClosed();
		}

		private void ReadFromStream( int count )
		{

			if( m_ReadState == 0 )
			{
				
				// This is the start of some new data
				// Create a new buffer stream
				m_BufferStream = new MemoryStream();
				m_ReadState = 1;
			}

			if( m_ReadState == 1 )
			{
				// write the data into the stream
				m_BufferStream.Write( m_ClientBuffer, 0, count );

				if( m_BufferStream.Length >= 4 )
				{
					// We have at least our header
					// so get the data length
					m_BufferStream.Position = 0;
					m_DataLength = Serialization.ReadLength( m_BufferStream ) + 4;
					m_BufferStream.Seek( 0, SeekOrigin.End );

					// indicate that we have the length and 
					// now need to read the remainder of the data
					m_ReadState = 2;
				}
			}
			else if( m_ReadState == 2 ) 
			{
				// We are reading Packet data
				m_BufferStream.Write( m_ClientBuffer, 0, count );
			}

			if( m_ReadState == 2 && m_BufferStream.Length >= m_DataLength )
			{
				// Deserialize the data into an object
				m_BufferStream.Position = 0;
				object msg = Serialization.ReadObject( m_BufferStream );

				// Signal the arrival of an object
				OnMessageReceived( msg );

				// Reset our state to indicate we're looking for
				// a new object
				m_ReadState = 0;

				// Now we need to handle any overflow data
				if( m_BufferStream.Length > m_DataLength ) 
				{
					m_BufferStream.Position = m_DataLength; //TODO: Longs required for larger data sets?
					m_BufferStream.Read( m_ClientBuffer, 0, (int)(m_BufferStream.Length - m_DataLength) );
					ReadFromStream( (int)(m_BufferStream.Length - m_DataLength) );
				}
			}
		}

		protected void OnMessageReceived( object data )
		{
			if( DataReceived != null ) DataReceived( data, this );
		}

		protected void OnConnectionClosed()
		{	
			Close( true ); // probably remote close so allow retry
			if( ConnectionClosed != null ) ConnectionClosed( this );
		}

		public PeerEndPoint RemoteEndPoint{ get { return m_PeerEndPoint; } }
		public string RemoteHost { get { return m_PeerEndPoint.Host; } }
		public int RemotePort { get { return m_PeerEndPoint.Port; } }

		public string ServentGuid
		{
			get { return m_ServentGuid; }
			set { m_ServentGuid = value; }
		}

		public string PeerGuid
		{
			get { return m_PeerEndPoint.Guid; }
			set { m_PeerEndPoint.Guid = value; }
		}

		public ServentStatus Status
		{
			get { return m_Status; }
			set { m_Status = value; }
		}

		public ServentRole Role
		{
			get { return m_Role; }
			set { m_Role = value; }
		}

		public new void Close( )
		{
			// local close request so should
			Close( false ); // prevent retry
		}

		public void Close( bool allowRetry )
		{
			try // closing servent network resources
			{  				
				lock( this ) // called from multiple threads
				{
					// if servent has not disconnected already
					if( m_Status != ServentStatus.Disconnected )
					{
						m_ClientStream.Close(); 
						base.Close(); // Close TcpClient						
						if( !allowRetry ) m_PeerEndPoint.MaxRetries = 0;
						m_Status = ServentStatus.Disconnected;
					}
				}				
			}
			catch( Exception e )
			{
				m_Status = ServentStatus.Disconnected;
				Log.Write( "Could not close the socket",
				TraceLevel.Warning, "Servent.Close", e );				
			}
		}
	}

	public enum ServentStatus : int
	{
		Initializing,
		Connecting,
		Connected,
		Disconnected
	}

	public enum ServentRole : int
	{
		Client,
		Server
	}

}