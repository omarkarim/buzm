using System;
using System.Diagnostics;
using System.Collections;
using Buzm.Network.Sockets;
using Buzm.Utility;
using NUnit.Framework;

namespace Buzm.Network.Packets
{
	[Serializable] public class Packet
	{
		// Serialized vars
		private string m_Data;
		private int m_HopsToLive;
		private string m_HiveGuid;		
		private string m_PacketGuid;
		private bool m_ForwardMatched;
		private bool m_ForwardUnmatched;
		private PeerEndPoint[] m_Origin;
		private PeerEndPoint[] m_Destination;
		
		// These vars won't be sent over network
		[NonSerialized] protected Servent m_Sender;
		[NonSerialized] protected Servent m_Receiver;
		[NonSerialized] private const int DEFAULT_HTL = 15;
		[NonSerialized] private const int HIVE_MATCH_GAIN = 3;
		// HTL default is hardcoded so end users cannot alter
		
		public Packet( string text ) : this( text, "" ){}
		public Packet( string text, string hiveGuid )
		{
			m_Data = text;
			m_HiveGuid = hiveGuid;
			m_ForwardMatched = true;
			m_ForwardUnmatched = true;
			m_HopsToLive = DEFAULT_HTL;
			m_Origin = new PeerEndPoint[0];
			m_Destination = new PeerEndPoint[0];
			m_PacketGuid = Guid.NewGuid().ToString();
		}

		public virtual void Process( PeerManager mgr )
		{
			bool hiveMatch = false;
			Hashtable hives = mgr.HiveRegistry;
			
			if( ValidateReceiver() ) // if the receiving Servent is valid
			{				
				// if this packet has a specific destination send it there 
				if( m_Destination.Length > 0 ) mgr.SendToDestination( this );
				else // determine if the packet should be queued and forwarded
				{
					// detect hive match
					lock( hives.SyncRoot )
					{
						// Iterate the registered hives to see
						// if any match the hive for this packet
						foreach( string hiveGuid in hives.Keys )
						{
							if( hiveGuid.Equals( m_HiveGuid ) ) hiveMatch = true;
						}		
					}

					// If the packet matches amplify it's survival time
					// and add it to this peer's user interface msg queue
					if( hiveMatch ) // Bool is used to avoid nested locks
					{
						m_HopsToLive += HIVE_MATCH_GAIN;
						mgr.SetNextPacket( this );
					}

					m_HopsToLive--; // Decrement hop life for each forward
					if( ( m_HopsToLive > 0 ) // forward while more hops are allowed
						&& (( hiveMatch && m_ForwardMatched ) // forward with hive match
						|| ( !hiveMatch && m_ForwardUnmatched )) ) // forward without hive match
						mgr.SendToServents( this ); // use the PeerManager to foward packet to peers
				}
			}
		}

		/// <summary>Checks if the receiving Servent
		///  has been properly initialized </summary>
		protected bool ValidateReceiver( )
		{
			if( m_Receiver != null ) // a receiver was assigned to the packet
			{
				// if a welcome packet has already been received from Servent 
				if( m_Receiver.Status == ServentStatus.Connected ) return true;
				else
				{
					Log.Write( "Packet received prior to Welcome from: " + m_Receiver.RemoteHost + ":" + m_Receiver.RemotePort + " : " + ToString(), 
					TraceLevel.Verbose, "Packet.ValidateReceiver" );
				}
			}
			return false; // validation failed if the code reached this point
		}

		/// <summary> Add the peer's info to the path this 
		/// packet has taken across the network </summary>
		public void AppendOrigin( PeerEndPoint endPoint )
		{		
			// Fixed arrays are used to keep the serialization simple
			// The current endPoint is appended and the rest are copied
			PeerEndPoint[] newOrigin = new PeerEndPoint[m_Origin.Length + 1];
			newOrigin[newOrigin.Length - 1] = endPoint;
			m_Origin.CopyTo( newOrigin, 0 );				
			m_Origin = newOrigin;			
		}

		/// <summary> Returns end point array that leads
		/// back to where the packet came from </summary>
		public PeerEndPoint[] GetPathToOrigin( )
		{
			PeerEndPoint[] path = new PeerEndPoint[m_Origin.Length];
			m_Origin.CopyTo( path, 0 );
			Array.Reverse( path );
			return path;
		}

		/// <summary>Removes and returns first 
		/// destination Peer End Point</summary>
		public PeerEndPoint DequeueDestination( )
		{ 
			PeerEndPoint pep = null;
			int length = m_Destination.Length;

			if( length > 0 ) // only dequeue if this is not an empty array
			{	
				pep = m_Destination[0];
				PeerEndPoint[] newDest = new PeerEndPoint[length - 1];
				if( length > 1 ) Array.Copy( m_Destination, 1, newDest, 0, length - 1 );
				m_Destination = newDest;
			}
			return pep; // returns dequeued end point or null if destination is empty
		}

		public int HopsToLive
		{ 
			get { return m_HopsToLive; } 
			set { m_HopsToLive = value; }
		}
		
		public string HiveGuid
		{ 
			get { return m_HiveGuid; } 
			set { m_HiveGuid = value; }
		}

		public string PacketGuid
		{ 
			get { return m_PacketGuid; } 
			set { m_PacketGuid = value; }
		}

		/// <summary> Specifies if the 
		/// packet is forwarded when a
		/// matching hive exists </summary>
		public bool ForwardMatched
		{ 
			get { return m_ForwardMatched; } 
			set { m_ForwardMatched = value; }
		}

		/// <summary> Specifies if the 
		/// packet is forwarded when no
		/// matching hive exists </summary>
		public bool ForwardUnmatched
		{ 
			get { return m_ForwardUnmatched; } 
			set { m_ForwardUnmatched = value; }
		}

		public Servent Sender
		{ 
			get { return m_Sender; } 
			set { m_Sender = value; }
		}

		public Servent Receiver
		{ 
			get { return m_Receiver; } 
			set { m_Receiver = value; }
		}

		public PeerEndPoint[] Origin
		{ 
			get { return m_Origin; } 
			set { m_Origin = value; }
		}

		public PeerEndPoint[] Destination
		{ 
			get { return m_Destination; } 
			set { m_Destination = value; }
		}

		public override string ToString()
		{
			return m_Data;
		}

		#region NUnit Automated Test Cases

		[TestFixture] public class PacketTest
		{			
			[SetUp] public void SetUp(){ }
			[TearDown] public void TearDown(){ }

			[Test] public void DequeueDestinationTest()
			{
				Packet pkt = new Packet( "This is the DequeueDestination test packet" );
				PeerEndPoint[] peps = new PeerEndPoint[]{ new PeerEndPoint( "", 0, "0" ),
														  new PeerEndPoint( "", 0, "1" ),
														  new PeerEndPoint( "", 0, "2" )};
				pkt.Destination = peps;
				PeerEndPoint firstPep = pkt.DequeueDestination();

				// check returned item and array for appropriate values after dequeue
				Assertion.AssertEquals( "Incorrect item dequeued from array.", "0", firstPep.Guid );
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 2, pkt.Destination.Length );
				Assertion.AssertEquals( "Incorrect array element at index zero.", "1", pkt.Destination[0].Guid );
				Assertion.AssertEquals( "Incorrect array element at index one.", "2", pkt.Destination[1].Guid );

				// attempt to dequeue beyond bounds
				firstPep = pkt.DequeueDestination();
				Assertion.AssertEquals( "Incorrect item dequeued from array.", "1", firstPep.Guid );
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 1, pkt.Destination.Length );
				Assertion.AssertEquals( "Incorrect array element at index zero.", "2", pkt.Destination[0].Guid );

				firstPep = pkt.DequeueDestination();
				Assertion.AssertEquals( "Incorrect item dequeued from array.", "2", firstPep.Guid );
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 0, pkt.Destination.Length );
				
				firstPep = pkt.DequeueDestination();
				Assertion.AssertNull( "Expected null item to be returned.", firstPep );
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 0, pkt.Destination.Length );
			}
		}

		#endregion
	}
}