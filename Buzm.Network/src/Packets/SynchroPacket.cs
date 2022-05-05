using System;
using System.Threading;
using System.Collections;
using NUnit.Framework;
using Buzm.Network.Sockets;
using Buzm.Utility;

namespace Buzm.Network.Packets
{
	/// <summary> Encapsulates bi-directional synchronization between peers.
	/// Note: The algorithm assumes that any two connected network peers are 
	/// already synchronized or are in the process of synchronizing. </summary>
	[Serializable] public class SynchroPacket : Packet
	{
		private byte[] m_HiveSynchroHash;		// hive hash at packet origin
		private string[] m_RequestItemGuids;	// item guids at packet origin
		private string[] m_ResponseItemGuids;	// delta guids after comparison

		public SynchroPacket( string text, string hiveGuid ) : base( text, hiveGuid ) 
		{ 
			// Instruct peer network not to forward this packet when hive matches
			ForwardMatched = false; // forwarding will be handled by the UI thread
		}

		/// <summary>Process local guids against origin guids. Note: 
		/// the ContentManager thread will call base.Process</summary>
		/// <param name="mgr">PeerManager to synchronize through</param>
		/// <param name="localHiveHash">Merkle root hash for hive</param>
		/// <param name="localItemGuids">Local item guids for hive</param>
		/// <returns>Delta item guids that need to be synchronized</returns>
		public string[] Process( string[] localItemGuids, byte[] localHiveHash, PeerManager mgr )
		{
			// if packet is local 
			if( Origin.Length == 0 )
			{
				// populate local synchro hash
				m_HiveSynchroHash = localHiveHash;
				
				// if packet contains a valid sender forward it there
				if( m_Sender != null ) mgr.SendToServent( m_Sender, this );
				else mgr.SendToServents( this ); // else send to all peers
			}
			else // this SynchroPacket was received over the peer network
			{   
				// if packet contains response to an earlier synchro request
				if( m_ResponseItemGuids != null ) return m_ResponseItemGuids;
				else 
				{	
					// this is a forwarded synchro request from the Origin node that 
					if( m_RequestItemGuids != null ) // should contain an array of guids
					{
						// find delta items by removing any guids that are common to both arrays
						ArrayHelper.RemoveDuplicates( ref localItemGuids, ref m_RequestItemGuids );

						if( m_RequestItemGuids.Length > 0 ) // if Origin has items not found here 
						{
							// send response to Origin with specific items that are missing locally
							SynchroPacket respPkt = new SynchroPacket( "SynchroResponse", HiveGuid );
							respPkt.ResponseItemGuids = m_RequestItemGuids; 
							respPkt.Destination = GetPathToOrigin();
							mgr.SendToDestination( respPkt );	
						}

						// return array of guids that the origin needs
						// caller should send these items in individual 
						return localItemGuids; // packets to the Origin
					}
					else if( ( m_HiveSynchroHash != null ) // check synchro needed
						&& !ArrayHelper.AreEqual( localHiveHash, m_HiveSynchroHash ) )
					{
						// hashes did not match so send local item guids to Origin
						SynchroPacket reqPkt = new SynchroPacket( "SynchroContinue", HiveGuid );
						reqPkt.RequestItemGuids = localItemGuids;
						reqPkt.Destination = GetPathToOrigin();
						mgr.SendToDestination( reqPkt );
					}
				}
			}

			// no items need be sent to Origin so return
			return new string[0]; // an empty guid array
		}

		public string[] RequestItemGuids
		{ 
			get { return m_RequestItemGuids; } 
			set { m_RequestItemGuids = value; }
		}

		public string[] ResponseItemGuids
		{ 
			get { return m_ResponseItemGuids; } 
			set { m_ResponseItemGuids = value; }
		}

		public byte[] HiveSynchroHash
		{
			get { return m_HiveSynchroHash; }
			set { m_HiveSynchroHash = value; }
		}

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class SynchroPacketTest
		{
			// Hashtables to store test objects
			private Hashtable m_PeerManagers;
			private Hashtable m_PeerContents;
			private const int NET_SLEEP = 750;

			[SetUp] public void SetUp()
			{
				//********************************************************************************/
				// Create a test P2P network with two independent islands joined at one point.
				// Node naming scheme is island-position_server-port-suffix_single-digit-hive-ids
				// The port suffix will be added to 6000 to determine the actual server port used.
				// 
				//			   1T_03_2								 2T_13_1
				//				  |										|
				//				  |										|
				//	1L_01_1 <---1C_02---> 1R_05_12 ===>> 2L_11_1 ---> 2C_12---> 2R_15_2
				//				  |										|
				//				  |										|	
				//			   1B_04_2								 2B_14_1
				//
				//********************************************************************************/

				// Initialize test object tables
				m_PeerManagers = new Hashtable();
				m_PeerContents = new Hashtable();

				// Create test peers  for island 1 based on configuration
				m_PeerManagers.Add( "1L_01_1", new PeerManager( 6001, null ) );
				m_PeerManagers.Add( "1C_02", new PeerManager( 6002, null ) );
				m_PeerManagers.Add( "1T_03_2", new PeerManager( 6003, null ) );
				m_PeerManagers.Add( "1B_04_2", new PeerManager( 6004, null ) );
				m_PeerManagers.Add( "1R_05_12", new PeerManager( 6005, null ) );

				// Interconnect island 1
				GetPeer( "1C_02" ).CreateServentAsync( "localhost", 6001 );
				GetPeer( "1C_02" ).CreateServentAsync( "localhost", 6005 );
				GetPeer( "1T_03_2" ).CreateServentAsync( "localhost", 6002 );
				GetPeer( "1B_04_2" ).CreateServentAsync( "localhost", 6002 );

				// Create test peers  for island 2 based on configuration
				m_PeerManagers.Add( "2L_11_1", new PeerManager( 6011, null ) );
				m_PeerManagers.Add( "2C_12", new PeerManager( 6012, null ) );
				m_PeerManagers.Add( "2T_13_1", new PeerManager( 6013, null ) );
				m_PeerManagers.Add( "2B_14_1", new PeerManager( 6014, null ) );
				m_PeerManagers.Add( "2R_15_2", new PeerManager( 6015, null ) );
				
				// Interconnect island 2
				GetPeer( "2L_11_1" ).CreateServentAsync( "localhost", 6012 );
				GetPeer( "2C_12" ).CreateServentAsync( "localhost", 6013 );
				GetPeer( "2C_12" ).CreateServentAsync( "localhost", 6014 );
				GetPeer( "2C_12" ).CreateServentAsync( "localhost", 6015 );

				Thread.Sleep( 2000 ); // Wait for connections to complete
			}

			[TearDown] public void TearDown()
			{
				foreach( PeerManager mgr in m_PeerManagers.Values )
				{
					mgr.Close();
				}
				Thread.Sleep( 1000 ); // Wait for connections to close
			}

			[Test] public void ConnectSynchroTest( )
			{
				// Register hives for island 1
				RegisterHive( "1L_01_1", "1" );
				RegisterHive( "1T_03_2", "2" );
				RegisterHive( "1B_04_2", "2" );
				RegisterHive( "1R_05_12", "1" );
				RegisterHive( "1R_05_12", "2" );

				// Register hives for island 2
				RegisterHive( "2L_11_1", "1" );
				RegisterHive( "2T_13_1", "1" );
				RegisterHive( "2B_14_1", "1" );
				RegisterHive( "2R_15_2", "2" );

				// Clear any existing packet queues
				ClearPeerPackets(); // from all peers

				// Add random hive matched items
				AddItem( "1R_05_12", "1", "1R item - hive 1" );
				AddItem( "1R_05_12", "2", "1R item - hive 2" );
				AddItem( "2L_11_1", "1", "2L item - hive 1" );
				AddItem( "2R_15_2", "2", "2R item - hive 2" );

				// Create linear connections between nodes 1R and 2L
				GetPeer( "1R_05_12" ).CreateServentAsync( "localhost", 6011 );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag
				
				// Run synchro for 1R and 2L with dummy hash
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00 } );
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00 } );
				Thread.Sleep( NET_SLEEP ); // Simulate net lag

				// reply to SynchroRequest with SynchroContinue
				ProcessSynchroPacket( "2L_11_1", new byte[] { 0x01 } );
				Thread.Sleep( NET_SLEEP ); // Simulate net lag

				// reply to SynchroContinue with SynchroResponse
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00 } );
				Thread.Sleep( NET_SLEEP ); // Simulate net lag

				// reply to SynchroResponse with missing item
				ProcessSynchroPacket( "2L_11_1", new byte[] { 0x01 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag
				
				// 2L should have sent the one missing item to 1R
				Packet pkt = GetNextContentPacket( GetPeer( "1R_05_12" ) );
				Assert.AreEqual( "2L item - hive 1", pkt.ToString(), "1R got incorrect sync content from 2L" );

				// 2L should have sent the one missing item to 1L
				pkt = GetNextContentPacket( GetPeer( "1L_01_1" ) );
				Assert.AreEqual( "2L item - hive 1", pkt.ToString(), "1L got incorrect forwarded content from 2L" );

				// 1R should have sent the one missing item to 2L
				pkt = GetNextContentPacket( GetPeer( "2L_11_1" ) );
				Assert.AreEqual( "1R item - hive 1", pkt.ToString(), "2L got incorrect sync content from 1R" );

				// 1R should have sent the one missing item to 2T
				pkt = GetNextContentPacket( GetPeer( "2T_13_1" ) );
				Assert.AreEqual( "1R item - hive 1", pkt.ToString(), "2T got incorrect forwarded content from 1R" );
				
				// 1R should have sent the one missing item to 2B
				pkt = GetNextContentPacket( GetPeer( "2B_14_1" ) );
				Assert.AreEqual( "1R item - hive 1", pkt.ToString(), "2T got incorrect forwarded content from 1R" );

				// 1R should not have sent any other packet to 2L
				pkt = GetNextContentPacket( GetPeer( "2L_11_1" ) );
				Assert.IsNull( pkt, "2L received unexpected packet" );

				// Run synchro for 1R and 2R
				ProcessSynchroPacket( "2R_15_2", null );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// reply with SynchroResponse
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// reply with missing item
				ProcessSynchroPacket( "2R_15_2", null );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// 2R should have sent the one missing item to 1R
				pkt = GetNextContentPacket( GetPeer( "1R_05_12" ) );
				Assert.AreEqual( "2R item - hive 2", pkt.ToString(), "1R got incorrect sync content from 2R" );

				// 2R should have sent the one missing item to 1T
				pkt = GetNextContentPacket( GetPeer( "1T_03_2" ) );
				Assert.AreEqual( "2R item - hive 2", pkt.ToString(), "1R got incorrect sync content from 2R" );

				// 2R should have sent the one missing item to 1B
				pkt = GetNextContentPacket( GetPeer( "1B_04_2" ) );
				Assert.AreEqual( "2R item - hive 2", pkt.ToString(), "1R got incorrect sync content from 2R" );

				// 1R should have sent the one missing item to 2R
				pkt = GetNextContentPacket( GetPeer( "2R_15_2" ) );
				Assert.AreEqual( "1R item - hive 2", pkt.ToString(), "2R got incorrect sync content from 1R" );

				// 1R should not have sent any other packet to 2R
				pkt = GetNextContentPacket( GetPeer( "2R_15_2" ) );
				Assert.IsNull( pkt, "2R received unexpected packet" );

				// 1C should not have received any packet
				pkt = GetNextContentPacket( GetPeer( "1C_02" ) );
				Assert.IsNull( pkt, "1C received unexpected packet" );

				// 2C should not have received any packet
				pkt = GetNextContentPacket( GetPeer( "2C_12" ) );
				Assert.IsNull( pkt, "2C received unexpected packet" );
			}

			[Test] public void AddHiveSynchroTest( )
			{
				// Register hives for island 1 other than the
				// 1R_05_12 node which will be the synchro origin
				RegisterHive( "1L_01_1", "1" );
				RegisterHive( "1T_03_2", "2" );
				RegisterHive( "1B_04_2", "2" );

				// Register hives for island 2
				RegisterHive( "2L_11_1", "1" );
				RegisterHive( "2T_13_1", "1" );
				RegisterHive( "2B_14_1", "1" );
				RegisterHive( "2R_15_2", "2" );

				// Create linear connections between nodes 1R and 2L
				GetPeer( "1R_05_12" ).CreateServentAsync( "localhost", 6011 );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// Clear any existing packet queues
				ClearPeerPackets(); // from all peers

				// Register hives for synchro origin
				RegisterHive( "1R_05_12", "1" );
				RegisterHive( "1R_05_12", "2" );

				// Add random hive matched items
				AddItem( "1R_05_12", "1", "1R item - hive 1" );
				AddItem( "1R_05_12", "2", "1R item - hive 2" );
				AddItem( "1L_01_1", "1", "1L item - hive 1" );
				AddItem( "1T_03_2", "2", "1T item - hive 2" );
				AddItem( "1B_04_2", "2", "1B item - hive 2" );
				AddItem( "2L_11_1", "1", "2L item - hive 1" );
				AddItem( "2R_15_2", "2", "2R item - hive 2" );
				
				// Run synchro for 1R and 2L with dummy hash
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00 } );
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00 } );
				Thread.Sleep( NET_SLEEP ); // Simulate net lag

				// reply to SynchroRequest with SynchroContinue
				ProcessSynchroPacket( "2L_11_1", new byte[] { 0x01, 0x02 } );
				Thread.Sleep( NET_SLEEP ); // Simulate net lag

				// reply to SynchroContinue with SynchroResponse
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00 } );
				Thread.Sleep( NET_SLEEP ); // Simulate net lag

				// reply to SynchroResponse with missing item
				ProcessSynchroPacket( "2L_11_1", new byte[] { 0x01, 0x02 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag
				
				// 2L should have sent the one missing item to 1R
				Packet pkt = GetNextContentPacket( GetPeer( "1R_05_12" ) );
				Assert.AreEqual( "2L item - hive 1", pkt.ToString(), "1R got incorrect sync content from 2L" );

				// 1R should have sent the one missing item to 2L
				pkt = GetNextContentPacket( GetPeer( "2L_11_1" ) );
				Assert.AreEqual( "1R item - hive 1", pkt.ToString(), "2L got incorrect sync content from 1R" );

				// 1R should have sent the one missing item to 2T
				pkt = GetNextContentPacket( GetPeer( "2T_13_1" ) );
				Assert.AreEqual( "1R item - hive 1", pkt.ToString(), "2T got incorrect forwarded content from 1R" );
				
				// 1R should have sent the one missing item to 2B
				pkt = GetNextContentPacket( GetPeer( "2B_14_1" ) );
				Assert.AreEqual( "1R item - hive 1", pkt.ToString(), "2T got incorrect forwarded content from 1R" );

				// 1R should not have sent any other packet to 2L
				pkt = GetNextContentPacket( GetPeer( "2L_11_1" ) );
				Assert.IsNull( pkt, "2L received unexpected packet" );

				// Run synchro for 1R and 1L
				ProcessSynchroPacket( "1L_01_1", new byte[0] );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate lag

				// reply with SynchroResponse
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate lag

				// check queued 1L packet from previous synchro
				// 2L should have sent the one missing item to 1L
				pkt = GetNextContentPacket( GetPeer( "1L_01_1" ) );
				Assert.AreEqual( "2L item - hive 1", pkt.ToString(), "1L got incorrect forwarded content from 2L" );

				// complete synchro & reply with missing item
				ProcessSynchroPacket( "1L_01_1", new byte[0] );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate lag
				
				// 1L should have sent the one missing item to 1R
				pkt = GetNextContentPacket( GetPeer( "1R_05_12" ) );
				Assert.AreEqual( "1L item - hive 1", pkt.ToString(), "1R got incorrect forwarded content from 1L" );

				// 1L should have sent the one missing item to 2L
				pkt = GetNextContentPacket( GetPeer( "2L_11_1" ) );
				Assert.AreEqual( "1L item - hive 1", pkt.ToString(), "2L got incorrect forwarded content from 1L" );

				// 1L should have sent the one missing item to 2T
				pkt = GetNextContentPacket( GetPeer( "2T_13_1" ) );
				Assert.AreEqual( "1L item - hive 1", pkt.ToString(), "2T got incorrect forwarded content from 1L" );

				// 1L should have sent the one missing item to 2B
				pkt = GetNextContentPacket( GetPeer( "2B_14_1" ) );
				Assert.AreEqual( "1L item - hive 1", pkt.ToString(), "2B got incorrect forwarded content from 1L" );

				// 1R should have sent the one missing item to 1L
				pkt = GetNextContentPacket( GetPeer( "1L_01_1" ) );
				Assert.AreEqual( "1R item - hive 1", pkt.ToString(), "1L got incorrect sync content from 1R" );

				// 1R should not have sent any other packet to 1L
				pkt = GetNextContentPacket( GetPeer( "1L_01_1" ) );
				Assert.IsNull( pkt,"1L received unexpected packet" );
				
				// Run synchro for 1R and 2R
				ProcessSynchroPacket( "2R_15_2", new byte[] { 0x03 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// reply with SynchroResponse
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// reply with missing item
				ProcessSynchroPacket( "2R_15_2", new byte[] { 0x03 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// 2R should have sent the one missing item to 1R
				pkt = GetNextContentPacket( GetPeer( "1R_05_12" ) );
				Assert.AreEqual( "2R item - hive 2", pkt.ToString(), "1R got incorrect sync content from 2R" );

				// 2R should have sent the one missing item to 1B
				pkt = GetNextContentPacket( GetPeer( "1B_04_2" ) );
				Assert.AreEqual( "2R item - hive 2", pkt.ToString(), "1B got incorrect sync content from 2R" );

				// 1R should have sent the one missing item to 2R
				pkt = GetNextContentPacket( GetPeer( "2R_15_2" ) );
				Assert.AreEqual( "1R item - hive 2", pkt.ToString(), "2R got incorrect sync content from 1R" );

				// 1R should not have sent any other packet to 2R
				pkt = GetNextContentPacket( GetPeer( "2R_15_2" ) );
				Assert.IsNull( pkt, "2R received unexpected packet" );

				// Run synchro for 1R and 1T
				ProcessSynchroPacket( "1T_03_2", new byte[] { 0x04, 0x05 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// reply with SynchroResponse
				ProcessSynchroPacket( "1R_05_12", new byte[] { 0x00, 0x01 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// check queued 1T packet from previous synchro
				// 2R should have sent the one missing item to 1T
				pkt = GetNextContentPacket( GetPeer( "1T_03_2" ) );
				Assert.AreEqual( "2R item - hive 2", pkt.ToString(), "1T got incorrect sync content from 2R" );

				// complete synchro and reply with missing item
				ProcessSynchroPacket( "1T_03_2", new byte[] { 0x04, 0x05 } );
				Thread.Sleep( NET_SLEEP * 2 ); // Simulate net lag

				// 1T should have sent the one missing item to 1R
				pkt = GetNextContentPacket( GetPeer( "1R_05_12" ) );
				Assert.AreEqual( "1T item - hive 2", pkt.ToString(), "1R got incorrect sync content from 1T" );

				// 1T should have sent the one missing item to 2R
				pkt = GetNextContentPacket( GetPeer( "2R_15_2" ) );
				Assert.AreEqual( "1T item - hive 2", pkt.ToString(), "2R got incorrect sync content from 1T" );

				// 1T should not have sent any packet to 1B
				pkt = GetNextContentPacket( GetPeer( "1B_04_2" ) );
				Assert.IsNull( pkt, "1B received unexpected packet" );

				// 1R should have sent the one missing item to 1T
				pkt = GetNextContentPacket( GetPeer( "1T_03_2" ) );
				Assert.AreEqual( "1R item - hive 2", pkt.ToString(), "1T got incorrect sync content from 1R" );

				// 1R should not have sent any other packet to 1T
				pkt = GetNextContentPacket( GetPeer( "1T_03_2" ) );
				Assert.IsNull( pkt, "1T received unexpected packet" );

				// 1R should not have sent any packet to 1B
				pkt = GetNextContentPacket( GetPeer( "1B_04_2" ) );
				Assert.IsNull( pkt, "1B received unexpected packet" );

				// 1C should not have received any packet
				pkt = GetNextContentPacket( GetPeer( "1C_02" ) );
				Assert.IsNull( pkt, "1C received unexpected packet" );

				// 2C should not have received any packet
				pkt = GetNextContentPacket( GetPeer( "2C_12" ) );
				Assert.IsNull( pkt, "2C received unexpected packet" );
			}

			private Packet GetNextTypedPacket( PeerManager mgr, string typeName )
			{
				Packet pkt;				
				while( (pkt = mgr.GetNextPacket()) != null )
				{ if( pkt.GetType().Name == typeName ) return pkt; }
				return null;
			}

			private Packet GetNextContentPacket( PeerManager mgr )
			{
				return GetNextTypedPacket( mgr, "Packet" );
			}

			private SynchroPacket GetNextSynchroPacket( PeerManager mgr )
			{
				return (SynchroPacket)GetNextTypedPacket( mgr, "SynchroPacket" );
			}

			private void RegisterHive( string node, string hive )
			{
				ModelEventArgs e = new ModelEventArgs( hive, null );
				PeerManager mgr = (PeerManager)m_PeerManagers[node];
				mgr.HiveManager_HiveAdded( this, e ); // simulate event
				m_PeerContents.Add( node + ":" + hive, new ArrayList() );
			}

			private void AddItem( string node, string hive, string item )
			{
				ArrayList content = (ArrayList)m_PeerContents[node + ":" + hive];
				content.Add( item );
			}

			private string[] GetItems( string node, string hive )
			{
				ArrayList content = (ArrayList)m_PeerContents[node + ":" + hive];
				return (string[])content.ToArray( typeof(string) );
			}

			private PeerManager GetPeer( string node )
			{
				return ((PeerManager)m_PeerManagers[node]);
			}

			private void ClearPeerPackets( )
			{
				foreach( PeerManager mgr in m_PeerManagers.Values )
				{
					mgr.RemoveAllPackets(); // empty the packet queue
				}
			}	

			/// <summary> Manages the Hive synchronization request/response cycle. This
			/// code was duplicated from Buzm.Main to avoid a dll dependency </summary>
			private void ProcessSynchroPacket( string node, byte[] rootHash )
			{
				PeerManager mgr = GetPeer( node );
				SynchroPacket syncPkt = GetNextSynchroPacket( mgr );
				PeerEndPoint[] returnPath = syncPkt.GetPathToOrigin();
				
				string[] localItemGuids = GetItems( node, syncPkt.HiveGuid );
				string[] deltaItemGuids = syncPkt.Process( localItemGuids, rootHash, mgr );
				
				// iterate and send hive items
				for( int i=0; i < deltaItemGuids.Length; i++ )
				{
					Packet deltaPkt = new Packet( deltaItemGuids[i], syncPkt.HiveGuid );
					deltaPkt.Destination = returnPath; // fixed path 
					mgr.SendToDestination( deltaPkt );
				}
			}
		}

		#endif
		#endregion
	}
}