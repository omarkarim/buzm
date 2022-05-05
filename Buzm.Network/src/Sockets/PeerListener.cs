using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Collections;
using System.Diagnostics;
using Buzm.Utility;

namespace Buzm.Network.Sockets
{
	public class PeerListener
	{
		private int				m_Port;
		private Thread			m_ListenThread;
		private TcpListener		m_Listener;
		private IServentFactory m_ServentFactory;

		public PeerListener( int port, IServentFactory srvFactory )
		{ 		
			m_Port = port;
			m_ServentFactory = srvFactory;

			//TODO: Check if requested port is already in use
			m_Listener = new TcpListener( IPAddress.Any, port );
			m_ListenThread = new Thread( new ThreadStart( Listen ) );
			m_ListenThread.Start();
		}

		private void Listen()
		{
			Thread.CurrentThread.Name = "PeerListener on port: " + m_Port.ToString();
			m_Listener.Start();

			while( true ) // Enter infinite server loop
			{
				try // accepting incoming socket connections 
				{  
					Socket socket = m_Listener.AcceptSocket();			
					m_ServentFactory.CreateServent( socket );
				}
				catch( ThreadAbortException )
				{ 
					// Occurs normally when the thread is aborted during shutdown
					Log.Write( "Thread " + Thread.CurrentThread.Name + " was aborted",
					TraceLevel.Verbose, "PeerListener.Listen" );
				}
				catch( Exception e )
				{ 
					// Safety net for any unexpected exceptions that may occur
					Log.Write( "Unexpected exception while accepting connection",
					TraceLevel.Error, "PeerListener.Listen", e );
				}
			}
		}

		// TODO: Synchronize this method?
		public PeerEndPoint LocalEndPoint
		{ 
			get 
			{ 	
				string firstAddress;
				string name = Dns.GetHostName();
				IPHostEntry hostEntry = Dns.GetHostByName( name ); 
				IPAddress[] addresses = hostEntry.AddressList;

				// Note. This only returns the first IP address among many
				if( addresses.Length > 0 ) firstAddress = addresses[0].ToString();
				else firstAddress = IPAddress.Loopback.ToString();
				
				// The call is dynamic since the IP might change
				return new PeerEndPoint( firstAddress, m_Port );
			} 
		}

		/// <summary> Port server is listening on </summary>
		public int LocalPort { get { return m_Port; } }

		public void Close()
		{
			if( m_ListenThread != null )
			{
				m_ListenThread.Abort();
				m_Listener.Stop();
			}
		}
		
	}
}