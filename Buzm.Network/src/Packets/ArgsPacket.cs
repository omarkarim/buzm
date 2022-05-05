using System;
using System.Diagnostics;
using Buzm.Network.Sockets;
using Buzm.Utility;

namespace Buzm.Network.Packets
{
	/// <summary>Used for inter-process communication
	/// of command-line arguments if a second Buzm client
	/// starts while one is already running </summary>
	[Serializable] public class ArgsPacket : Packet
	{
		private bool m_ArgsReceived; 
		private string[] m_Arguments;

		public ArgsPacket() : this( new string[]{} ){ }
		public ArgsPacket( string[] args ) : base( "" )
		{
			m_Arguments = args;
			m_ArgsReceived = false;
		}

		public string[] Arguments
		{ 
			get { return m_Arguments; } 
			set { m_Arguments = value; }
		}

		public bool ArgsReceived
		{
			get { return m_ArgsReceived; }
			set { m_ArgsReceived = value; }
		}

		public override void Process( PeerManager mgr )
		{			
			if( m_Receiver != null )
			{
				mgr.SetNextPacket( this ); // raise args to the client
				ArgsPacket pkt = new ArgsPacket(); // empty pkt for reply
				
				pkt.ArgsReceived = true; // indicate that pkt was received
				mgr.SendToServent( m_Receiver, pkt, false ); // send reply
				
				Log.Write( "Received args from another Buzm process", 
				TraceLevel.Verbose, "ArgsPacket.Process" );
			}
		}
	}
}