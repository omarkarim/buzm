using System;
using System.Xml;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using Buzm.Network.Packets;
using System.Runtime.InteropServices;
using Buzm.Utility;
using NUnit.Framework;

namespace Buzm.Network.Sockets
{
	public delegate void NetworkChangedEventHandler( PeerManager mgr, Servent srv, bool async );
	
	/// <summary> Manages P2P content and connects </summary>
	public class PeerManager : INetworkManager, IServentFactory
	{
		private string			m_PeerGuid;
		private string			m_LoopbackAddress;
		private bool			m_CheckForNetwork;
		private bool			m_RegistryEnabled;
		private bool			m_NotifyUser;		

		private Hashtable		m_Servents;
		private Hashtable		m_HiveRegistry;
		private PeerListener	m_PeerListener;
		private Thread			m_ContentThread;
		private Thread			m_ConnectThread;
		private HashQueue		m_PacketHistory;
		private Queue			m_ConnectNodeQueue;
		private Queue			m_IncomingPktQueue;
		private Queue			m_RelevantPktQueue;
		private AppVersion		m_AppVersion;

		// Default constants
		private const int PACKET_CACHE_SIZE = 100;
		private const int CONTENT_CHECK_TIMEOUT = 300;
		private const int NET_CONNECTION_TIMEOUT = 15000;
		private const int NET_DISCONNECTED_TIMEOUT = 3000;
		
		//Used to redirect internal events to the UI thread
		public NetworkChangedEventHandler NetworkChanged;
		private NetworkChangedEventHandler m_OnNetworkChanged;
		private ISynchronizeInvoke m_SyncObject;

		public PeerManager( int serverPort, ISynchronizeInvoke sync )
		: this( serverPort, sync, new AppVersion() ){ }

		public PeerManager( ISynchronizeInvoke sync, AppVersion version ) 
		: this( Config.GetIntValue( "network/defaultPort", 0 ), sync, version ){ }

		public PeerManager( ISynchronizeInvoke sync ) : this( sync, new AppVersion() ){ }
		public PeerManager( int serverPort, ISynchronizeInvoke sync, AppVersion version )
		{ 	
			m_SyncObject = sync;	
			m_NotifyUser = true;

			m_Servents = new Hashtable();
			m_HiveRegistry = new Hashtable();
			
			m_ConnectNodeQueue = new Queue();
			m_IncomingPktQueue = new Queue();
			m_RelevantPktQueue = new Queue();			
			
			m_PeerGuid = Guid.NewGuid().ToString();
			if( version != null ) m_AppVersion = version;
			else throw new ArgumentNullException( "version" );

			m_LoopbackAddress = IPAddress.Loopback.ToString();
			m_CheckForNetwork = Config.GetBoolValue( "network/checkNetwork", false );
			m_RegistryEnabled = Config.GetBoolValue( "preferences/registry/enabled", false );

			m_OnNetworkChanged = new NetworkChangedEventHandler( OnNetworkChanged );
			m_PacketHistory = new HashQueue( Config.GetIntValue( "network/packetCache", PACKET_CACHE_SIZE ) );
			
			// Initialize socker server for incoming traffic
			m_PeerListener = new PeerListener( serverPort, this );
			Log.Write( TraceLevel.Info, "PeerListener on port " + serverPort.ToString(), "PeerManager.PeerManager" );

			// Add list of initial nodes to connect with
			XmlNodeList nodes = Config.GetValues( "nodes/node" );
			if( nodes != null )
			{
				foreach( XmlNode node in nodes )
				{
					CreateServentAsync( Config.GetValue( node, "host" ), 
										Config.GetIntValue( node, "port" ) );
				}
			}

			// Register list of initial hives to process
			XmlNodeList hives = Config.GetValues( "hives/hive" );
			if( hives != null )
			{
				foreach( XmlNode hive in hives )
				{
					RegisterHive( Config.GetValue( hive, "guid" ) );
				}
			}

			// Start the connection management thread
			m_ConnectThread = new Thread( new ThreadStart( ManagePeerConnections ) );
			m_ConnectThread.Start();

			// Start the content management thread
			m_ContentThread = new Thread( new ThreadStart( ManagePeerContent ) );
			m_ContentThread.Start();
		}

		#region Content Management Methods

		private void ManagePeerContent( )
		{	
			int count;
			Packet pkt;
			
			Thread.CurrentThread.Name = "ContentManager";
			while( true ) // Enter infinite content loop
			{
				try // reading content packets from queue
				{  
					count = 0; // reset from last run
					lock( m_IncomingPktQueue.SyncRoot )
					{ count = m_IncomingPktQueue.Count; }

					// static loop avoids nested lock
					for( int i = 0; i < count; i++ )
					{
						pkt = null; // reset reference
						lock( m_IncomingPktQueue.SyncRoot )
						{ pkt = (Packet)m_IncomingPktQueue.Dequeue(); }	
				
						// Only process received packets that have not been seen before
						if( ( pkt != null ) && (!m_PacketHistory.Contains( pkt.PacketGuid )) )
						{	
							pkt.Process( this ); // Delegate to subclasses
							m_PacketHistory.Enqueue( pkt.PacketGuid, null );
						}
					}
					
					// Wait for new packets to arrive
					Thread.Sleep( CONTENT_CHECK_TIMEOUT );
				}
				catch( InvalidOperationException e )
				{ 
					// Occurs if there are no more packets to dequeue
					Log.Write( "Some Packets dequeued by another thread",
					TraceLevel.Warning, "PeerManager.ManagePeerContent", e );
				}
				catch( ThreadAbortException )
				{ 
					// Occurs normally when the thread is aborted during shutdown
					Log.Write( "Thread " + Thread.CurrentThread.Name + " was aborted",
					TraceLevel.Verbose, "PeerManager.ManagePeerContent" );
				}
				catch( Exception e )
				{ 
					// Safety net for any unexpected errors that may occur
					Log.Write( "Could not read or process packet from queue",
					TraceLevel.Error, "PeerManager.ManagePeerContent", e );
				}
			}
		}

		/// <summary> Add a packet matching this peer's
		/// registered hives to the message queue </summary>
		public void SetNextPacket( Packet pkt )
		{
			lock( m_RelevantPktQueue.SyncRoot )
			{
				m_RelevantPktQueue.Enqueue( pkt );
			}
		}
		
		/// <summary> Retreive the next packet matching this 
		/// peer's registered hives from the queue </summary>
		public Packet GetNextPacket()
		{
			Packet pkt = null;
			lock( m_RelevantPktQueue.SyncRoot )
			{ 
				if( m_RelevantPktQueue.Count > 0 )
				{	pkt = (Packet)m_RelevantPktQueue.Dequeue(); }
			}
			return pkt;
		}

		/// <summary> Removes received 
		/// packets from queue </summary>
		public void RemoveAllPackets()
		{
			lock( m_RelevantPktQueue.SyncRoot )
			{ 
				m_RelevantPktQueue.Clear();				
			}
		}

		/// <summary> Sends a packet to connected servents. If the packet was
		/// received through a servent it will not be sent to that one </summary>
		public void SendToServents( Packet pkt )
		{
			SetPacketOrigin( pkt ); // add peer to route
			m_PacketHistory.Enqueue( pkt.PacketGuid, null );
		
			// Send packet to servents
			lock( m_Servents.SyncRoot )
			{
				foreach( Servent srv in m_Servents.Values )
				{ 
					if( ( !srv.Equals( pkt.Receiver ) ) && 
						(  srv.Status == ServentStatus.Connected ) ) srv.Send( pkt );
				}
			}
		}

		/// <summary> Sends a packet to the specified servent. This 
		/// method does not check if the handshake is completed </summary>
		public void SendToServent( Servent srv, Packet pkt, bool async )
		{
			m_PacketHistory.Enqueue( pkt.PacketGuid, null );
			SetPacketOrigin( pkt ); // add peer to route
			srv.Send( pkt, async ); // send to servent
		}

		/// <summary> Sends a packet to the specified servent. 
		/// This method does not check if the handshake has been 
		/// completed. Send is asynchronous by default. </summary>
		public void SendToServent( Servent srv, Packet pkt )
		{
			SendToServent( srv, pkt, true );
		}

		/// <summary> Sends a packet to the next Peer End  
		/// Point specified in its Destination array </summary>
		public void SendToDestination( Packet pkt )
		{
			// remove next end point from packet destination
			PeerEndPoint nextEndPoint = pkt.DequeueDestination();
			
			// if next destination exists and contains a peer GUID
			if( (nextEndPoint != null) && (nextEndPoint.Guid != "") ) 
			{
				Servent destSrv = null;
				lock( m_Servents.SyncRoot )
				{	// iterate to match destination by GUID
					foreach( Servent srv in m_Servents.Values )
					{ 
						if( ( srv.Status == ServentStatus.Connected ) && 
							( srv.PeerGuid == nextEndPoint.Guid ) )
						{ destSrv = srv; break; }
					}
				}
				if( destSrv != null ) SendToServent( destSrv, pkt );
			}
		}

		/// <summary>Appends end point for local
		/// peer to packet origin array</summary>
		private void SetPacketOrigin( Packet pkt )
		{
			PeerEndPoint pep = m_PeerListener.LocalEndPoint;
			pep.Version = m_AppVersion.ToString(); // set version
			pep.Guid = m_PeerGuid; // set unique local peer guid
			pkt.AppendOrigin( pep ); // add to origin array
		}

		public void RegisterHive( string hiveGuid )
		{
			lock( m_HiveRegistry.SyncRoot )
			{
				if( !m_HiveRegistry.Contains( hiveGuid ) )
				{
					m_HiveRegistry.Add( hiveGuid, null ); 
				}
			}
		}

		public void UnregisterHive( string hiveGuid )
		{
			lock( m_HiveRegistry.SyncRoot )
			{
				if( m_HiveRegistry.Contains( hiveGuid ) )
				{
					m_HiveRegistry.Remove( hiveGuid ); 
				}
			}
		}

		public void HiveManager_HiveAdded( object sender, ModelEventArgs e )
		{
			string hiveGuid = e.ModelGuid; // guid in args
			if( hiveGuid != null ) // if valid guid is found
			{
				RegisterHive( hiveGuid ); // add to hive table & initiate synchro
				SynchroPacket pkt = new SynchroPacket( "SynchroRequest", hiveGuid );
				SetNextPacket( pkt ); // queue packet for client to populate 
			}
		}

		public void HiveManager_HiveRemoved( object sender, ModelEventArgs e )
		{
			string hiveGuid = e.ModelGuid; // guid in args
			if( hiveGuid != null ) // if valid guid is found
			{
				UnregisterHive( hiveGuid ); // remove from hive table
			}
		}

		/// <summary>Adds a synchronization packet to the 
		/// relevant packet queue for all registered hives. 
		/// The queue's client is reponsible for populating the 
		/// packets and sending the request to the servent</summary>
		public void SynchronizeHives( Servent srv )
		{
			SynchroPacket pkt;
			string[] hiveGuids;

			// if this servent is a client 
			if( srv.Role == ServentRole.Client )
			{				
				// copy hive guids to array
				lock( m_HiveRegistry.SyncRoot )
				{
					hiveGuids = new string[m_HiveRegistry.Keys.Count];
					m_HiveRegistry.Keys.CopyTo( hiveGuids, 0 );
				}

				// TODO: Add Thread.Sleep to loop
				// create SynchroPacket for each hive
				foreach( string hiveGuid in hiveGuids )
				{
					pkt = new SynchroPacket( "SynchroRequest", hiveGuid );
					pkt.Sender = srv; // bind packet to specific destination
					SetNextPacket( pkt ); // queue packet for client to populate
				}		
			}
		}

		private void Servent_DataReceived( object data, Servent srv )
		{
			((Packet)data).Receiver = srv;
			lock( m_IncomingPktQueue.SyncRoot ) { m_IncomingPktQueue.Enqueue( data ); }
		}

		#endregion

		// TODO: Move to PeerFactory
		#region Connection Management Methods

		private void ManagePeerConnections( )
		{
			int count;
			PeerEndPoint pep;

			Thread.CurrentThread.Name = "ConnectionManager";
			while( true ) // Enter infinite connection loop
			{
				try // connecting to peer end points in the queue
				{
					count = 0; // reset from last run
					lock( m_ConnectNodeQueue.SyncRoot )
					{ count = m_ConnectNodeQueue.Count; }

					if( count > 0 ) // if we have connections to make
					{
						// wait for the network before making connections
						if( !m_CheckForNetwork || NetworkInterface.GetIsNetworkAvailable() )
						{
							// static loop avoids nested lock
							for( int i = 0; i < count; i++ )
							{
								pep = null; // reset reference
								lock( m_ConnectNodeQueue.SyncRoot )
								{ pep = (PeerEndPoint)m_ConnectNodeQueue.Dequeue(); }
								if( pep != null ) CreateServent( pep );
							}
						}
						else // wait briefly for network to connect
						{							
							Thread.Sleep( NET_DISCONNECTED_TIMEOUT );
							continue; // skip to next iteration
						}
					}

					// wait longer since the queue is empty
					Thread.Sleep( NET_CONNECTION_TIMEOUT );

				}
				catch( InvalidOperationException e )
				{ 
					// Occurs if there are no more peers to dequeue
					Log.Write( "Connection was dequeued by another thread",
						TraceLevel.Warning, "PeerManager.ManagePeerConnections", e );
				}
				catch( ThreadInterruptedException )
				{
					// Another thread tried to wake this one
					Log.Write( "Connection thread was woken from sleep",
						TraceLevel.Verbose, "PeerManager.ManagePeerConnections" );
				}
				catch( ThreadAbortException )
				{ 
					// Occurs normally when the thread is aborted during shutdown
					Log.Write( "Thread " + Thread.CurrentThread.Name + " was aborted",
						TraceLevel.Verbose, "PeerManager.ManagePeerConnections" );
				}
				catch( Exception e )
				{ 
					// Safety net for any unexpected errors that may occur
					Log.Write( "Could not read or process connection from queue",
						TraceLevel.Error, "PeerManager.ManagePeerConnections", e );
				}
			}
		}

		/// <summary> Create servent based on incoming socket
		/// connection allocated by the PeerListener </summary>
		public void CreateServent( Socket socket )
		{
			Servent srv = new Servent( socket );
			RegisterServent( srv );
		}

		public void CreateServent( string host, int port )
		{
			CreateServent( new PeerEndPoint( host, port ) );
		}

		public void CreateServent( PeerEndPoint pep )
		{
			bool success = false;
			try // connecting to specified peer end point for the
			{  // first time or if the retry interval has elapsed
				
				if( pep.ShouldConnectNow() )
				{
					Log.Write( "Connection attempt " + pep.RetryCount.ToString() 
							   + " to peer: " + pep.Host + ":" + pep.Port.ToString(),
							   TraceLevel.Verbose, "PeerManager.CreateServent" );

					Servent srv = new Servent( pep ); // establish connection
					RegisterServent( srv ); // add servent to collection
					success = true; // prevent retry attempt
				}
			}
			catch( Exception e )
			{
				// TODO: Raise connection failed event and notify UI of connection earlier
				Log.Write( "Could not connect to: " + pep.Host + ":" + pep.Port.ToString(),
				TraceLevel.Warning, "PeerManager.CreateServent", e );
			}
			finally
			{	
				if( !success  ) // connection skipped or failed
				{	// and more retries should be attempted
					if( pep.RetryCount < pep.MaxRetries )
					{	
						// requeue peer end point for future connection attempts
						lock( m_ConnectNodeQueue.SyncRoot ){ m_ConnectNodeQueue.Enqueue( pep ); }
					}
					else
					{
						Log.Write( "Maximum retries exceeded for: " + pep.Host + ":" + pep.Port.ToString(),
						TraceLevel.Warning, "PeerManager.CreateServent" );
					}
				}
			}
		}

		public void CreateServentAsync( string host, int port )
		{
			CreateServentAsync( new PeerEndPoint( host, port ) );
		}

		public void CreateServentAsync( PeerEndPoint endPoint )
		{
			lock( m_ConnectNodeQueue.SyncRoot ){ m_ConnectNodeQueue.Enqueue( endPoint ); }
			if( m_ConnectThread != null ) m_ConnectThread.Interrupt(); // Wake connect thread

		}

		private void RegisterServent( Servent srv )
		{
			// Bind to servent events
			srv.DataReceived += new DataReceivedEventHandler(Servent_DataReceived);
			srv.ConnectionClosed += new ConnectionClosedEventHandler(Servent_ConnectionClosed);
			
			lock( m_Servents.SyncRoot )
			{ 
				// Add servent to shared collection
				if( !m_Servents.Contains( srv.ServentGuid ) ) 
					 m_Servents.Add( srv.ServentGuid, srv );
			}
			
			// Start servent
			srv.BeginReceive();
			OnNetworkChanged( this, srv, true );

			// if this servent is a client 
			// initialize handshake sequence
			if( srv.Role == ServentRole.Client )
			{
				WelcomePacket pkt = new WelcomePacket( "StartHandshake" );
				SendToServent( srv, pkt, false );
			}
		}

		public void UnregisterServent( Servent srv )
		{
			lock( m_Servents.SyncRoot )
			{
				if( m_Servents.Contains( srv.ServentGuid ) )
				{
					m_Servents.Remove( srv.ServentGuid ); 
					srv.DataReceived -= new DataReceivedEventHandler(Servent_DataReceived);
					srv.ConnectionClosed -= new ConnectionClosedEventHandler(Servent_ConnectionClosed);					
				}
			}
			OnNetworkChanged( this, srv, true );
		}

		/// <summary>Retreives a servent based on
		/// the guid of the remote peer </summary>
		public Servent GetServent( string remoteGuid )
		{
			Servent matchSrv = null;
			if( remoteGuid != "" )
			{
				lock( m_Servents.SyncRoot )
				{
					foreach( Servent srv in m_Servents.Values )
					{ 
						if( ( srv.PeerGuid == remoteGuid ) &&
							( srv.Status == ServentStatus.Connected ) )
						{
							matchSrv = srv;
							break;
						}
					}
				}
			}
			return matchSrv; // null if not found
		}

		/// <summary> Currently this method only checks if an 
		/// IP address has been assigned to any of the network
		/// interfaces. The Windows API function IsNetworkAlive
		/// is the correct way to detect network state but in testing
		/// it had sporadic results with WLAN connections. </summary>
		public bool IsNetworkConnected( )
		{
			PeerEndPoint pep = m_PeerListener.LocalEndPoint;
			if( pep.Host != m_LoopbackAddress ) return true;
			else return false; // No IP has been assigned
		}

		/// <summary> Returns the number of servents
		/// that are connected and initialized </summary>
		public int ConnectionCount( )
		{
			int count = 0; 
			lock( m_Servents.SyncRoot )
			{
				foreach( Servent srv in m_Servents.Values )
				{ 
					// check if handshake has completed for the servent
					if( srv.Status == ServentStatus.Connected ) count++;
				}
			}
			return count; // return zero if no connected servent was found
		}

		public void OnNetworkChanged( PeerManager mgr, Servent srv, bool async )
		{
			// Recurse delegate to notify GUI asynchronously of network changed event
			if( async && (m_SyncObject != null) ) m_SyncObject.BeginInvoke( m_OnNetworkChanged, new object[]{ mgr, srv, false } ); 
			else{ if( NetworkChanged != null ) NetworkChanged( mgr, srv, async ); }
		}

		private void Servent_ConnectionClosed( Servent srv )
		{	
			UnregisterServent( srv );
			Log.Write( "Lost connection to: " + srv.RemoteHost + ":" + srv.RemotePort.ToString(),
			TraceLevel.Verbose, "PeerManager.Servent_ConnectionClosed" );

			// if this servent was the client 
			if( srv.Role == ServentRole.Client )
			{	
				PeerEndPoint pep = srv.RemoteEndPoint;
				if( pep.MaxRetries > 0 ) // if retry allowed
				{
					pep.Guid = ""; // remote guid might change
					pep.ResetRetryStats(); // clear connect history 
					CreateServentAsync( pep ); // and try to reconnect
				}
			}
		}

		#endregion

		public void Close()
		{
			lock( m_Servents.SyncRoot )
			{
				foreach( Servent srv in m_Servents.Values ){ srv.Close(); }		
				Log.Write( TraceLevel.Verbose, "All servents are disconnected", "PeerManager.Close" );
				m_Servents.Clear();
			}

			if( m_PeerListener != null )
			{
				m_PeerListener.Close();
				m_PeerListener = null;
			}

			if( m_ConnectThread != null )
			{
				m_ConnectThread.Abort();
				m_ConnectThread.Join();
			}

			if( m_ContentThread != null )
			{
				m_ContentThread.Abort();
				m_ContentThread.Join();
			}
		
		}	

		// Public properties
		public string PeerGuid { get { return m_PeerGuid; } }
		public int Port { get { return m_PeerListener.LocalPort; } }
		public Hashtable HiveRegistry { get { return m_HiveRegistry; } }
		public HashQueue PacketHistory { get { return m_PacketHistory; } }

		/// <summary>INetworkManager property that determines if the user
		/// should be alerted to packets received through this network </summary>
		public bool NotifyUser
		{
			get { return m_NotifyUser; }
			set { m_NotifyUser = value; }
		}

		/// <summary>Determines if Registry requests received
		/// by this peer can be handled locally or not </summary>
		public bool RegistryEnabled
		{
			get { return m_RegistryEnabled; }
			set { m_RegistryEnabled = value; }
		}

		/// <summary>Encapsulates version information
		/// for the currently running instance</summary>
		public AppVersion AppVersion
		{
			get { return m_AppVersion; }
			set { m_AppVersion = value; }
		}
		
		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class PeerManagerTest
		{
			private const int MAX_MESSAGES = 100;
			private PeerManager m_ServerManager;
			private PeerManager m_ClientManager;
			private PeerManager m_TransitManager;

			[SetUp] public void SetUp()
			{
				// Create a few test peers
				m_ServerManager  = new PeerManager( 6022, null );
				m_ClientManager  = new PeerManager( 6023, null );
				m_TransitManager = new PeerManager( 6024, null );
				
				// Create linear connections between them through aliases
				m_ClientManager.CreateServentAsync( Dns.GetHostName(), 6022 );
				m_TransitManager.CreateServentAsync( IPAddress.Loopback.ToString(), 6022 );
				
				// Register hives with overlap
				m_ServerManager.RegisterHive( "1" );
				m_ServerManager.RegisterHive( "2" );
				m_ClientManager.RegisterHive( "1" );
				m_ClientManager.RegisterHive( "3" );
				m_TransitManager.RegisterHive( "3" );
				m_ClientManager.RegisterHive( "4" );				
				m_ServerManager.RegisterHive( "4" );
				m_TransitManager.RegisterHive( "4" );

				// Simulate net lag
				Thread.Sleep( 1000 );
			}

			[TearDown] public void TearDown()
			{
				m_ClientManager.Close();
				m_ServerManager.Close();
				m_TransitManager.Close();

				// Simulate net lag
				Thread.Sleep( 1000 );
			}

			[Test] public void SimulateTraffic()
			{				
				Packet message;				
				for( int x=0; x < MAX_MESSAGES; x++ )
				{
					message = new Packet( "Server says hello: " + x.ToString(), "1" );
					m_ServerManager.SendToServents( message );

					message = new Packet( "Client says hello: " + x.ToString(), "1" );
					m_ClientManager.SendToServents( message );
				}	
			}

			[Test] public void ExchangePackets( )
			{
				Packet clientInMsg, serverInMsg, transitInMsg;
				Packet clientOutMsg, serverOutMsg;				

				// Reset peer manager state
				m_ClientManager.RemoveAllPackets();
				m_ServerManager.RemoveAllPackets();

				// Send outgoing messages with matching hives
				serverOutMsg = new Packet( "Server sends packet", "1" );
				m_ServerManager.SendToServents( serverOutMsg );
				clientOutMsg = new Packet( "Client sends packet", "1" );
				m_ClientManager.SendToServents( clientOutMsg );

				// Simulate net lag
				Thread.Sleep( 1000 );
				
				// Check for incoming messages
				clientInMsg = GetNextContentPacket( m_ClientManager );
				serverInMsg = GetNextContentPacket( m_ServerManager );

				// Check for message matches
				Assertion.AssertEquals( "Client did not receive correct string from server with matched hives.", serverOutMsg.ToString(), clientInMsg.ToString() );
				Assertion.AssertEquals( "Server did not receive correct string from client with matched hives.", clientOutMsg.ToString(), serverInMsg.ToString() );

				// Send outgoing messages with mismatching hives
				serverOutMsg = new Packet( "Server sends packet", "2" );
				m_ServerManager.SendToServents( serverOutMsg );
				clientOutMsg = new Packet( "Client sends packet", "3" );
				m_ClientManager.SendToServents( clientOutMsg );

				// Simulate net lag
				Thread.Sleep( 1000 );
				
				// Check for incoming messages
				clientInMsg  = GetNextContentPacket( m_ClientManager );
				serverInMsg  = GetNextContentPacket( m_ServerManager );
				transitInMsg = GetNextContentPacket( m_TransitManager );

				// No message should have been received by client and server this time, so check for null
				Assertion.AssertNull( "Client received a packet despite mismatched hives.", clientInMsg );
				Assertion.AssertNull( "Server received a packet despite mismatched hives.", serverInMsg );

				// Transit peer should have received a packet from the client through forwarding
				Assertion.AssertEquals( "Transit peer did not receive correct forwarded packet from client.", clientOutMsg.ToString(), transitInMsg.ToString() );
				Assertion.AssertEquals( "Packet reported incorrect first hop (client manager) port number.",  6023, ((PeerEndPoint)transitInMsg.Origin[0]).Port );
				Assertion.AssertEquals( "Packet reported incorrect second hop (server manager)port number.", 6022, ((PeerEndPoint)transitInMsg.Origin[1]).Port );

				// Create a triangular connection between the peers
				m_TransitManager.CreateServentAsync( "localhost", 6023 );

				// Simulate net lag
				Thread.Sleep( 1000 );

				// Send outgoing message on triangular network
				clientOutMsg = new Packet( "Client sends packet", "3" );
				m_ClientManager.SendToServents( clientOutMsg );

				// Simulate net lag
				Thread.Sleep( 1000 );

				// Check for incoming messages
				clientInMsg  = GetNextContentPacket( m_ClientManager );
				serverInMsg  = GetNextContentPacket( m_ServerManager );

				// No message should have been received by client and server again, so check for null
				Assertion.AssertNull( "Client received it's own packet on triangular network.", clientInMsg );
				Assertion.AssertNull( "Server received a packet despite mismatched hives.", serverInMsg );

				// Check transit peer for multiple messages
				transitInMsg = GetNextContentPacket( m_TransitManager );
				transitInMsg = GetNextContentPacket( m_TransitManager );

				// The second message should be null unless it was received and processed twice
				Assertion.AssertNull( "Transit manager processed same packet twice on triangular network.", transitInMsg );
			}

			[Test] public void SynchroForwarding()
			{	
				Packet clientOutMsg;
				Packet serverInMsg, transitInMsg;

				// Reset peer manager states
				m_ClientManager.RemoveAllPackets();
				m_ServerManager.RemoveAllPackets();
				m_TransitManager.RemoveAllPackets();

				// Send out message with all peers having registered hive "4"
				clientOutMsg = new SynchroPacket( "Client sends synchro packet", "4" );				
				m_ClientManager.SendToServents( clientOutMsg );

				// Simulate net lag
				Thread.Sleep( 1500 );
				
				// Check for incoming messages
				serverInMsg  = GetNextTypedPacket( m_ServerManager, "SynchroPacket" );
				transitInMsg = GetNextTypedPacket( m_TransitManager, "SynchroPacket" );

				// Server should have received the synchro packet but it should not have been forwarded to the transit peer
				Assertion.AssertEquals( "Server peer did not receive correct synchro packet from client.", clientOutMsg.ToString(), serverInMsg.ToString() );
				Assertion.AssertNull( "Transit peer incorrectly received a forwarded synchro packet from the server.", transitInMsg );

				// Register hive bypassing server
				m_ClientManager.RegisterHive( "5" );
				m_TransitManager.RegisterHive( "5" );

				// Send out message with all peers having registered hive "5"
				clientOutMsg = new SynchroPacket( "Client sends synchro packet", "5" );
				m_ClientManager.SendToServents( clientOutMsg );

				// Simulate net lag
				Thread.Sleep( 1500 );
				
				// Check for incoming messages
				serverInMsg  = GetNextTypedPacket( m_ServerManager, "SynchroPacket" );
				transitInMsg = GetNextTypedPacket( m_TransitManager, "SynchroPacket" );

				// Server should have forwarded the packet to the transit manager without queueing it locally
				Assertion.AssertNull( "Server peer incorrectly received an unmatched synchro packet from the client.", serverInMsg );
				Assertion.AssertEquals( "Transit peer did not receive expected synchro packet from client.", clientOutMsg.ToString(), transitInMsg.ToString() );
			}

			[Test] public void DestinationForwarding()
			{	
				Packet clientOutMsg;
				Packet serverInMsg, transitInMsg;	
				PeerEndPoint serverEndPoint, transitEndPoint;			

				// Reset peer manager states
				m_ClientManager.RemoveAllPackets();
				m_ServerManager.RemoveAllPackets();
				m_TransitManager.RemoveAllPackets();
				
				// Setup end points with GUIDs only - host and port are not needed
				serverEndPoint  = new PeerEndPoint( "", 0, m_ServerManager.PeerGuid );
				transitEndPoint = new PeerEndPoint( "", 0, m_TransitManager.PeerGuid );

				// Send out message along a specific path for registered hive "4"
				clientOutMsg = new Packet( "Client sends packet to destination", "4" );	
				clientOutMsg.Destination = new PeerEndPoint[]{ serverEndPoint, transitEndPoint };
				m_ClientManager.SendToDestination( clientOutMsg );

				// Simulate net lag
				Thread.Sleep( 1000 );
				
				// Check for incoming messages
				serverInMsg  = GetNextContentPacket( m_ServerManager );
				transitInMsg = GetNextContentPacket( m_TransitManager );

				// Server should have forwarded packet to transit peer without queueing it locally
				Assertion.AssertNull( "Server peer incorrectly received a packet intended for the transit peer.", serverInMsg );
				Assertion.AssertEquals( "Transit peer did not receive correct destination packet from client.", clientOutMsg.ToString(), transitInMsg.ToString() );
			}

			[Test] public void SimulateReconnect()
			{				
				Packet clientInMsg, serverInMsg;
				Packet clientOutMsg, serverOutMsg;	

				// Shutdown server 
				m_ServerManager.Close();
				Thread.Sleep( 1000 );

				// Reset client packet queue
				m_ClientManager.RemoveAllPackets();

				// Create new server - client should reconnect
				// automatically in approximately 15-20 seconds
				m_ServerManager = new PeerManager( 6022, null );
				m_ServerManager.RegisterHive( "1" );
				m_ServerManager.RegisterHive( "2" );
				m_ServerManager.RegisterHive( "4" );
				Thread.Sleep( 18000 );
			
				// Send outgoing messages with matching hives
				serverOutMsg = new Packet( "Server sends packet", "1" );
				m_ServerManager.SendToServents( serverOutMsg );
				clientOutMsg = new Packet( "Client sends packet", "1" );
				m_ClientManager.SendToServents( clientOutMsg );

				// Simulate net lag
				Thread.Sleep( 1000 );
				
				// Check for incoming messages
				clientInMsg = GetNextContentPacket( m_ClientManager );
				serverInMsg = GetNextContentPacket( m_ServerManager );

				// Check for message matches
				Assertion.AssertEquals( "Client did not receive correct string from server with matched hives.", serverOutMsg.ToString(), clientInMsg.ToString() );
				Assertion.AssertEquals( "Server did not receive correct string from client with matched hives.", clientOutMsg.ToString(), serverInMsg.ToString() );
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
		}

		#endif
		#endregion

	}

}