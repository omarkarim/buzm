using System;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using Buzm.Network.Sockets;
using NUnit.Framework;
using Buzm.Utility;

namespace Buzm.Network.Packets
{
	/// <summary> Encapsulates simple handshake process </summary>
	[Serializable] public class WelcomePacket : Packet
	{
		private string m_Link; // download link for new version
		private string m_Message; // human readable support message
		private int m_PeerSupport; // peer VersionSupport reply

		public WelcomePacket( string text ) : base( text )
		{
			m_Link = String.Empty; // set empty link
			m_Message = String.Empty; // set empty message
			HopsToLive = 1; // transmit to neighbours only
		}

		// TODO: Close any old/hung connections that
		// might still be open to the incoming servent
		// or PeerManager.SendToDesination() could fail
		public override void Process( PeerManager mgr )
		{
			Servent receiver = base.Receiver; // save ref
			if( ( receiver != null ) && ( Origin.Length > 0 ) )
			{
				PeerEndPoint originPoint = Origin[0];
				string originGuid = originPoint.Guid;

				// If the original sender is myself
				// Note: have to avoid infinite loop
				if( originGuid.Equals( mgr.PeerGuid ) )
				{
					receiver.Close();
					mgr.UnregisterServent( receiver );
					Log.Write( "Tried connecting to myself: " + originGuid, 
					TraceLevel.Verbose,	"WelcomePacket.Process" );
				}
				else
				{
					// cleanup hung connection to the same peer
					// Note: have to avoid infinite reconnect loop
					/*Servent hungSrv = mgr.GetServent( originGuid );
					if( hungSrv != null )
					{		
							hungSrv.Close(); 
							mgr.UnregisterServent( hungSrv );
							Log.Write( "Killed hung connection: " + originGuid, 
							TraceLevel.Verbose,	"WelcomePacket.Process" );
					}*/

					// complete handshake sequence for server
					if( receiver.Role == ServentRole.Server )
					{
						WelcomePacket pkt = new WelcomePacket( "CompleteHandshake" );
						bool supported = pkt.SetPeerVersionSupport( originPoint, mgr );
						
						mgr.SendToServent( receiver, pkt, false ); // respond to client
						if( supported ) ActivateServent( receiver, originPoint, mgr );
					}
					else if( receiver.Role == ServentRole.Client )
					{
						// if server indicated that it is compatible
						if( ProcessPeerVersionSupport( receiver, mgr ) )
						{
							ActivateServent( receiver, originPoint, mgr );
							mgr.SynchronizeHives( receiver ); // sync
						}
					}
				}
				
				Log.Write( "Welcome packet received from v" + originPoint.Version + " @" 
				+ receiver.RemoteHost + ":" + receiver.RemotePort + " - " + ToString(), 
				TraceLevel.Verbose, "WelcomePacket.Process" );
			}
		}

		/// <summary>enables servent for communication and notifies listeners</summary>
		private void ActivateServent( Servent srv, PeerEndPoint origin, PeerManager mgr )
		{
			srv.PeerGuid = origin.Guid; // save remote peer guid
			srv.Status = ServentStatus.Connected; // enable servent
			mgr.OnNetworkChanged( mgr, srv, true ); // notify listeners
		}

		/// <summary>Checks the peer app version against local app version
		/// to determine if they are compatible with each other and populates
		/// the WelcomePacket with the appropriate support meta data </summary>
		/// <returns>True if the peers are compatible, otherwise false</returns>
		private bool SetPeerVersionSupport( PeerEndPoint origin, PeerManager mgr )
		{
			AppVersion appVersion = mgr.AppVersion;
			if( appVersion != null ) // if version obj exists
			{
				VersionSupport support; // check if peer is supported and set meta data
				support = appVersion.CheckSupport( origin.Version, out m_Link, out m_Message );
				
				m_PeerSupport = (int)support; // cast to int for serialization

				if( support == VersionSupport.Unsupported ) return false;
				else return true; // probably can connect to peer
			}
			return true; // allow connection by default
		}

		/// <summary>Checks the support meta data that was populated by
		/// SetPeerVersionSupport and takes appropriate action </summary>
		/// <returns>True if peers are compatible, otherwise false</returns>
		private bool ProcessPeerVersionSupport( Servent srv, PeerManager mgr )
		{
			// cast int received from peer to VersionSupport value 
			VersionSupport support = (VersionSupport)m_PeerSupport;
			
			if( support == VersionSupport.Supported ) return true;
			else if ( support == VersionSupport.Unsupported )
			{
				Log.Write( "Terminating unsupported peer @"
				+ srv.RemoteHost + ":" + srv.RemotePort, 
				TraceLevel.Verbose,	"WelcomePacket" );

				srv.Close(); // close connection
				mgr.UnregisterServent( srv );
				mgr.SetNextPacket( this );
				return false;
			}
			else // deprecated or unknown version 
			{  // so delegate to user interface

				mgr.SetNextPacket( this );
				return true;
			}
		}

		public string Link
		{ 
			get { return m_Link; } 
			set { m_Link = value; }
		}

		public string Message
		{ 
			get { return m_Message; } 
			set { m_Message = value; }
		}

		public int PeerSupport
		{ 
			get { return m_PeerSupport; } 
			set { m_PeerSupport = value; }
		}

		#region NUnit Automated Test Cases

		[TestFixture] public class WelcomePacketTest
		{
			private ConsoleListener m_Listener;
			private PeerManager m_ClientManager;
			private PeerManager m_ServerManager;
			
			private const int NET_TIMEOUT = 1000;
			private const int SERVER_PORT = 6022;

			[SetUp] public void SetUp()
			{
				// bind to NUnit console listener
				m_Listener = new ConsoleListener();				
				Trace.Listeners.Add( m_Listener );
				Log.TraceLevel = TraceLevel.Verbose;
				
				// create a few test peers
				m_ClientManager  = new PeerManager( 6021, null );
				m_ServerManager  = new PeerManager( SERVER_PORT, null );
				m_ClientManager.CreateServentAsync( "localhost", SERVER_PORT );

				// simulate net lag
				Thread.Sleep( NET_TIMEOUT );
			}

			[TearDown] public void TearDown()
			{
				m_ClientManager.Close();
				m_ServerManager.Close();

				// remove NUnit console listener
				Trace.Listeners.Remove( m_Listener );
			}

			[Test] public void HandshakeTest( )
			{
				string clientGuid = m_ClientManager.PeerGuid;
				string serverGuid = m_ServerManager.PeerGuid;

				Servent client = m_ClientManager.GetServent( serverGuid );
				Servent server = m_ServerManager.GetServent( clientGuid );
 
				Assert.IsNotNull( client, "Client did not receive welcome from server" );
				Assert.IsNotNull( server, "Server did not receive welcome from client" );				
			}

			[Test, Ignore("Logic on TODO list")] 
			public void HungConnectionTest( )
			{
				string clientGuid = m_ClientManager.PeerGuid;
				string serverGuid = m_ServerManager.PeerGuid;

				// retreive first connection and ensure it's active
				Servent client = m_ClientManager.GetServent( serverGuid );
				Assertion.AssertNotNull( "Client did not connect to server", client );

				// try connecting again
				m_ClientManager.CreateServentAsync( IPAddress.Loopback.ToString(), SERVER_PORT );
				Thread.Sleep( NET_TIMEOUT ); // simulate net lag
 
				// check to see if first connection was closed as a result of second one
				Assertion.Assert( "Hung connection was not closed", client.Status == ServentStatus.Disconnected );

				// check to see if reconnect succeeded
				client = m_ClientManager.GetServent( serverGuid );
				Assertion.AssertNotNull( "Client did not reconnect to server", client );
				Assertion.Assert( "New connection not completed", client.Status == ServentStatus.Connected );
			}
			
			[Test] public void VersionSupportTest( )
			{
				string clientGuid = m_ClientManager.PeerGuid;
				string serverGuid = m_ServerManager.PeerGuid;
				CleanupServent( serverGuid, m_ClientManager );

				// load local config file for the Buzm assembly
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly );

				// set specific config version numbers to run tests against
				Config.SetValue( "versionSupport/supported/version", "2.0" );
				Config.SetValue( "versionSupport/deprecated/version", "1.0" );
				Config.SetValue( "versionSupport/unsupported/version", "0.5" );

				// load server app version with new config
				AppVersion serverVersion = new AppVersion();
				m_ServerManager.AppVersion = serverVersion;

				// set client app version to a supported value
				AppVersion clientVersion = new AppVersion( "3.0" );
				m_ClientManager.AppVersion = clientVersion;

				m_ClientManager.CreateServentAsync( "localhost", SERVER_PORT );
				Thread.Sleep( NET_TIMEOUT ); // allow connection to complete

				Servent client = m_ClientManager.GetServent( serverGuid );
				Servent server = m_ServerManager.GetServent( clientGuid );
				WelcomePacket pkt = (WelcomePacket)m_ClientManager.GetNextPacket();
 
				Assert.IsNotNull( client, "Client should have connected to Server with Supported version" );
				Assert.IsNotNull( server, "Server should have connected to Client with Supported version" );
				Assert.IsNull( pkt, "No WelcomePacket should have been reported by Client when Supported" );

				// disconnect so we can try Unsupported version
				CleanupServent( serverGuid, m_ClientManager );

				// set client app version to Unsupported value
				clientVersion = new AppVersion( "0.75" );
				m_ClientManager.AppVersion = clientVersion;

				m_ClientManager.CreateServentAsync( "localhost", SERVER_PORT );
				Thread.Sleep( NET_TIMEOUT ); // allow connection to complete

				client = m_ClientManager.GetServent( serverGuid );
				server = m_ServerManager.GetServent( clientGuid );
				pkt = (WelcomePacket)m_ClientManager.GetNextPacket();
 
				Assert.IsNull( client, "Client should not have connected to Server with Unsupported version" );
				Assert.IsNull( server, "Server should not have connected to Client with Unsupported version" );
								
				Assert.IsNotNull( pkt, "WelcomePacket should have been reported by Client when Unsupported" );
				Assert.AreEqual( VersionSupport.Unsupported, (VersionSupport)pkt.PeerSupport, "Expected Unsupported" );
				Assert.IsTrue( pkt.Message != String.Empty, "WelcomePacket should contain some Message when Unsupported" );
				Assert.IsTrue( pkt.Link != String.Empty, "WelcomePacket should contain some Link when Unsupported" );

				// disconnect so we can try Deprecated version
				CleanupServent( serverGuid, m_ClientManager );

				// set client app version to Deprecated value
				clientVersion = new AppVersion( "1.0" );
				m_ClientManager.AppVersion = clientVersion;

				m_ClientManager.CreateServentAsync( "localhost", SERVER_PORT );
				Thread.Sleep( NET_TIMEOUT ); // allow connection to complete

				client = m_ClientManager.GetServent( serverGuid );
				server = m_ServerManager.GetServent( clientGuid );
				pkt = (WelcomePacket)m_ClientManager.GetNextPacket();
 
				Assert.IsNotNull( client, "Client should have connected to Server with Deprecated version" );
				Assert.IsNotNull( server, "Server should have connected to Client with Deprecated version" );

				Assert.IsNotNull( pkt, "WelcomePacket should have been reported by Client when Deprecated" );
				Assert.AreEqual( VersionSupport.Deprecated, (VersionSupport)pkt.PeerSupport, "Expected Deprecated" );
				Assert.IsTrue( pkt.Message != String.Empty, "WelcomePacket should contain some Message when Deprecated" );
				Assert.IsTrue( pkt.Link != String.Empty, "WelcomePacket should contain some Link when Deprecated" );

				pkt = (WelcomePacket)m_ServerManager.GetNextPacket();
				Assert.IsNull( pkt, "No WelcomePacket should have been reported by Server in any scenario" );

				// unload configuration or other nunit tests
				Config.UnloadConfig(); // will see it as well
			}

			private void CleanupServent( string srvGuid, PeerManager mgr )
			{
				Servent srv = mgr.GetServent( srvGuid  ); 
				if( srv != null ) // if connection exists
				{
					srv.Close(); // close connection
					mgr.UnregisterServent( srv );
					Thread.Sleep( NET_TIMEOUT );
				}
			}
		}

		#endregion
	}
}