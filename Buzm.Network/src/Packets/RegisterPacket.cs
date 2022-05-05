using System;
using System.Threading;
using System.Diagnostics;
using Buzm.Network.Sockets;
using NUnit.Framework;
using Buzm.Utility;

namespace Buzm.Network.Packets
{
	[Serializable] public class RegisterPacket : Packet
	{
		private int m_ActionCode;
		private int m_ResultCode;
		private string m_ActionGuid;
		private string m_ResultMessage;	

		public RegisterPacket( string text, int actionCode ) : base( text ) 
		{ 	
			m_ActionGuid = "";
			m_ResultMessage = "";
			m_ActionCode = actionCode;			
		}

		public RegisterPacket( string text, int resultCode, string resultMsg ) : base( text ) 
		{ 	
			m_ActionGuid = "";
			m_ResultCode = resultCode;
			m_ResultMessage = resultMsg;
		}

		public override void Process( PeerManager mgr )
		{
			if( ValidateReceiver() ) // if the receiving Servent is valid
			{				
				// if the packet has a specific destination send it there 
				if( Destination.Length > 0 ) mgr.SendToDestination( this );
				else // determine if the packet must be queued or forwarded
				{
					if( m_ResultCode == 0 ) // is an unprocessed request
					{
						// and this peer has a registry then queue packet
						if( mgr.RegistryEnabled ) mgr.SetNextPacket( this );
						else // forward along to other peers for processing
						{							
							HopsToLive = HopsToLive - 1; // decrement hops
							if( HopsToLive > 0 ) mgr.SendToServents( this );
						}
					}
					// this should be a processed result that has returned to the peer 
					else mgr.SetNextPacket( this ); // which made the original request
				}
			}				
		}

		public int ActionCode
		{ 
			get { return m_ActionCode; } 
			set { m_ActionCode = value; }
		}

		public string ActionGuid
		{ 
			get { return m_ActionGuid; } 
			set { m_ActionGuid = value; }
		}

		public int ResultCode
		{ 
			get { return m_ResultCode; } 
			set { m_ResultCode = value; }
		}

		public string ResultMessage
		{ 
			get { return m_ResultMessage; } 
			set { m_ResultMessage = value; }
		}

		#region NUnit Automated Test Cases

		[TestFixture] public class RegisterPacketTest
		{
			private PeerManager m_RemoteManager;
			private PeerManager m_ServerManager;
			private PeerManager m_ClientManager;
			private PeerManager m_TransitManager;

			[SetUp] public void SetUp()
			{
				// Create a few test peers
				m_ClientManager  = new PeerManager( 6021, null );
				m_TransitManager = new PeerManager( 6022, null );
				m_ServerManager  = new PeerManager( 6023, null );
				m_RemoteManager  = new PeerManager( 6024, null );

				// Configure server as the registry
				m_ServerManager.RegistryEnabled = true;
				m_RemoteManager.RegistryEnabled = true;
				
				// Create linear connections between the peers
				m_ClientManager.CreateServentAsync( "localhost", 6022 );
				m_TransitManager.CreateServentAsync( "localhost", 6023 );
				m_ServerManager.CreateServentAsync( "localhost", 6024 );

				// Simulate net lag
				Thread.Sleep( 1500 );
			}

			[TearDown] public void TearDown()
			{
				m_ClientManager.Close();
				m_ServerManager.Close();
				m_RemoteManager.Close();
				m_TransitManager.Close();
			}

			[Test] public void RegisterRouting( )
			{
				RegisterPacket clientInMsg, serverInMsg;
				RegisterPacket transitInMsg, remoteInMsg;
				RegisterPacket clientOutMsg, serverOutMsg;	

				// create register packet from client
				clientOutMsg = new RegisterPacket( "Request Register", 1 );
				m_ClientManager.SendToServents( clientOutMsg );

				// Simulate net lag
				Thread.Sleep( 1000 );
				
				// Check for incoming messages
				transitInMsg = GetNextRegisterPacket( m_TransitManager );
				serverInMsg  = GetNextRegisterPacket( m_ServerManager );
				remoteInMsg  = GetNextRegisterPacket( m_RemoteManager ); 
				
				// Check for correct message values
				Assertion.AssertNull( "Transit manager got a packet despite having no registry running", transitInMsg );
				Assertion.AssertEquals( "Server did not receive register packet from client", clientOutMsg.ToString(), serverInMsg.ToString() );
				Assertion.AssertNull( "Remote manager got register packet despite server having handled it already", remoteInMsg );

				// create register response from server
				serverOutMsg = new RegisterPacket( "Request Response", 1, "" );
				serverOutMsg.Destination = serverInMsg.GetPathToOrigin();
				m_ServerManager.SendToDestination( serverOutMsg );
				
				// Simulate net lag
				Thread.Sleep( 1000 );

				// Check for incoming messages
				transitInMsg = GetNextRegisterPacket( m_TransitManager );
				clientInMsg  = GetNextRegisterPacket( m_ClientManager );
				
				// Check for correct message values
				Assertion.AssertNull( "Transit manager got a packet despite the packet being sent to a specific destination", transitInMsg );
				Assertion.AssertEquals( "Client did not receive register response from server", serverOutMsg.ToString(), clientInMsg.ToString() );
			}

			private Packet GetNextTypedPacket( PeerManager mgr, string typeName )
			{
				Packet pkt;				
				while( (pkt = mgr.GetNextPacket()) != null )
				{ if( pkt.GetType().Name == typeName ) return pkt; }
				return null;
			}

			private RegisterPacket GetNextRegisterPacket( PeerManager mgr )
			{
				return (RegisterPacket)GetNextTypedPacket( mgr, "RegisterPacket" );
			}
		}

		#endregion
	}
}