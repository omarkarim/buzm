using System;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using Buzm.Network.Sockets;
using Buzm.Network.Packets;

namespace Buzm.Graphing
{
	/// <summary> Test class for the graphing engine. This 
	/// code is definitely not production ready :) </summary>
	public class Form1 : System.Windows.Forms.Form
	{
		private const int MIN_PORT = 6025;
		private const int MAX_PEERS = 15;
		private const int MAX_HIVES = 3;
		private const int MAX_HIVE_ID = 6;
		private const int MAX_SIBLINGS = 4;

		private Random m_Randomizer;
		private SortedList m_PeerRegistry;
		private PeerManager m_RootManager;
		private NetworkGraphView m_NetworkGraphView;
		private System.Windows.Forms.Timer m_GraphTimer;
		private System.ComponentModel.IContainer components;		

		public Form1()
		{			
			InitializeComponent();
			m_Randomizer = new Random();
			m_PeerRegistry = new SortedList();

			// Create a root node with random local hives
			m_RootManager = new PeerManager( MIN_PORT, null );
			string rootId = ConfigurePeer( m_RootManager );
			m_PeerRegistry.Add( rootId, m_RootManager );
			m_NetworkGraphView.AddRootNode( rootId );
			
			// Test connecting a node to itself
			m_RootManager.CreateServentAsync( "localhost", MIN_PORT );

			// Create random network						
			PeerManager parent, child;			
			string parentId, childId;
			int parentIndex, childIndex;
			
			// Create random parent child pairs
			for( int port = (MIN_PORT + 1); port < (MIN_PORT + MAX_PEERS); port++ )
			{
				child = new PeerManager( port, null );
				childId  = ConfigurePeer( child );

				parentIndex = m_Randomizer.Next( 0, m_PeerRegistry.Count );
				parentId = (string)m_PeerRegistry.GetKey( parentIndex );
				parent = (PeerManager)m_PeerRegistry[ parentId ];	

				m_PeerRegistry.Add( childId, child );
				parent.CreateServentAsync( "localhost", child.Port );
				m_NetworkGraphView.AddNode( parentId, childId );
			}

			// Create random sibling pairs
			for( int i = 0; i < MAX_SIBLINGS; i++ )
			{
				parentIndex = m_Randomizer.Next( 0, m_PeerRegistry.Count );
				parentId = (string)m_PeerRegistry.GetKey( parentIndex );
				parent = (PeerManager)m_PeerRegistry[ parentId ];	

				childIndex = m_Randomizer.Next( 0, m_PeerRegistry.Count );
				childId = (string)m_PeerRegistry.GetKey( childIndex );
				child = (PeerManager)m_PeerRegistry[ childId ];	

				parent.CreateServentAsync( "localhost", child.Port );
				m_NetworkGraphView.AddNode( parentId, childId );
			}

			// Start message loop
			m_GraphTimer.Start();            
		}

		private string ConfigurePeer( PeerManager mgr )
		{
			int hiveId;
			string hivesTitle = " {";
			string peerTitle = "";

			for( int i=0; i < MAX_HIVES; i++ )
			{	
				hiveId = m_Randomizer.Next( 0, MAX_HIVE_ID );
				mgr.RegisterHive( hiveId.ToString() );
				hivesTitle += hiveId.ToString() + ",";
			}

			peerTitle = mgr.Port.ToString() + hivesTitle + "} " + mgr.PeerGuid;
			return peerTitle; // Return string used for unique Id and node header
		}

		private void m_GraphTimer_Tick(object sender, System.EventArgs e)
		{
			Packet pkt;
			string origin;
			string peerId;			
			PeerManager peer; 
		
			// Select a random peer on the network
			int rndPeerIndex = m_Randomizer.Next( 0, m_PeerRegistry.Count );
			peerId = (string)m_PeerRegistry.GetKey( rndPeerIndex );
			peer   = (PeerManager)m_PeerRegistry[ peerId ];		

			// Select a random hive for that peer
			int counter = 0;
			string rndHiveGuid = "1";
			int rndHiveIndex = m_Randomizer.Next( 0, peer.HiveRegistry.Count );
			
			foreach( string hiveGuid in peer.HiveRegistry.Keys )
			{
				if( counter == rndHiveIndex ){ rndHiveGuid = hiveGuid; break; }
				counter++;
			}

			// Create test packet with random hive and send to selected peer
			pkt    = new Packet( "Got Packet: ", rndHiveGuid );
			peer.SendToServents( pkt );	

			// Simulate Network lag
			Thread.Sleep( 500 );

			// Notify view of the outgoing packet
			m_NetworkGraphView.SetNodeStatus( peerId, "Sent Packet: " + " hid-" + pkt.HiveGuid
											+ " pid-" + pkt.PacketGuid , Color.Green ); 

			// Process received peer content	
			foreach( DictionaryEntry entry in m_PeerRegistry )
			{ 
				peer = (PeerManager)entry.Value;
				while( (pkt = peer.GetNextPacket()) != null )
				{ 
					origin = "";
					foreach( PeerEndPoint ep in pkt.Origin )
					{
						origin += ep.Port.ToString() + ":";
					}

					m_NetworkGraphView.SetNodeStatus( entry.Key.ToString(), pkt.ToString() 
						+ origin + " hid-" + pkt.HiveGuid + " htl-" + pkt.HopsToLive.ToString() 
						+ " pid-" + pkt.PacketGuid, Color.Blue ); 
				}
			}
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.m_NetworkGraphView = new Buzm.Graphing.NetworkGraphView();
			this.m_GraphTimer = new System.Windows.Forms.Timer(this.components);
			this.SuspendLayout();
			// 
			// m_NetworkGraphView
			// 
			this.m_NetworkGraphView.AutoScroll = true;
			this.m_NetworkGraphView.AutoScrollMargin = new System.Drawing.Size(10, 10);
			this.m_NetworkGraphView.BackColor = System.Drawing.SystemColors.ControlDarkDark;
			this.m_NetworkGraphView.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_NetworkGraphView.Location = new System.Drawing.Point(0, 0);
			this.m_NetworkGraphView.Name = "m_NetworkGraphView";
			this.m_NetworkGraphView.Size = new System.Drawing.Size(704, 510);
			this.m_NetworkGraphView.TabIndex = 0;
			// 
			// m_GraphTimer
			// 
			this.m_GraphTimer.Interval = 15000;
			this.m_GraphTimer.Tick += new System.EventHandler(this.m_GraphTimer_Tick);
			// 
			// Form1
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.AutoScroll = true;
			this.ClientSize = new System.Drawing.Size(704, 510);
			this.Controls.Add(this.m_NetworkGraphView);
			this.Name = "Form1";
			this.Text = "Buzm Network Viewer";
			this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
			this.Closing += new System.ComponentModel.CancelEventHandler(this.Form1_Closing);
			this.ResumeLayout(false);

		}
		#endregion

		private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e) 
		{
			foreach( PeerManager mgr in m_PeerRegistry.Values ){ mgr.Close(); }				
			Debug.WriteLine( true, "All peer managers are closed." );
			m_PeerRegistry.Clear();
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			Application.Run(new Form1());
		}
	}
}
