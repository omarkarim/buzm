using System;
using System.Drawing;
using System.Collections;
using Buzm.Network.Sockets;

namespace Buzm
{
	/// <summary>StatusBarPanel that displays network status</summary>
	public class NetStatusPanel : System.Windows.Forms.StatusBarPanel 
	{
		private Icon m_ConnectedIcon;
		private Icon m_DisconnectedIcon;
		private Hashtable m_PendingNodeList;
		private Hashtable m_ConnectNodeList;
		
		private const int DEFAULT_WIDTH = 200;
		private const int MAX_CONNECT_LINES = 25;
		private const int MAX_PENDING_LINES = 25;
		private const string DISCONNECTED = " Disconnected";		
		private const string CONNECTED_SINGLE = " Connected";
		private const string CONNECTED_MULTIPLE = " Connections";
		private const string CONNECT_TIP_PREFIX = "Connected to ";		
		private const string PENDING_TIP_PREFIX = "Connecting to ";
		private const string PENDING_TIP_SUFFIX = "...";		
		private const string CONNECT_TIP_SUFFIX = "";
		private const string MORE_NODES = "...";

		public NetStatusPanel() 
		{	
			m_PendingNodeList = new Hashtable();
			m_ConnectNodeList = new Hashtable();
			
			//TODO: Move network icons to the resources collection
			m_ConnectedIcon = new Icon( "Data/Resources/Net_Connect.ico" );
			m_DisconnectedIcon = new Icon( "Data/Resources/Net_Disconnect.ico" );

			Icon = m_DisconnectedIcon; // set default icon
			Text = DISCONNECTED; // set default text
			Width = DEFAULT_WIDTH; // size panel
		}

		public void PeerManager_NetworkChanged( PeerManager mgr, Servent srv, bool async )
		{	
			// add and remove servent from approprate hashtable based on status
			switch ( srv.Status ) // messages might be received out of order
			{
				case ServentStatus.Connecting:
					AddServent( m_PendingNodeList, srv );
					RemoveServent( m_ConnectNodeList, srv );
					break;
				case ServentStatus.Connected:
					AddServent( m_ConnectNodeList, srv );
					RemoveServent(  m_PendingNodeList, srv );
					break;
				case ServentStatus.Disconnected:
					RemoveServent(  m_PendingNodeList, srv );
					RemoveServent( m_ConnectNodeList, srv );
					break;
			}
			// set feedback text for status bar panel
			int connectCount = m_ConnectNodeList.Count; 			
			if( connectCount == 0 ) // if no connections
			{				
				Text = DISCONNECTED;
				Icon = m_DisconnectedIcon;
			}
			else // if one or more connections
			{
				Icon = m_ConnectedIcon; // set panel icon
				if( connectCount == 1 ) Text = CONNECTED_SINGLE;
				else Text = " " + connectCount.ToString() + CONNECTED_MULTIPLE;
			}			
			SetToolTipText(); // populate tool tip with network addresses
		}

		private void SetToolTipText()
		{
			string tipText = String.Empty; // set default 
			
			tipText += BuildServentListText( m_ConnectNodeList, 
			MAX_CONNECT_LINES, CONNECT_TIP_PREFIX, CONNECT_TIP_SUFFIX );

			tipText += BuildServentListText( m_PendingNodeList, 
			MAX_PENDING_LINES, PENDING_TIP_PREFIX, PENDING_TIP_SUFFIX );

			ToolTipText = tipText.Trim(); // remove trailing newline
		}

		private string BuildServentListText( Hashtable serventList, 
			int maxLines, string linePrefix, string lineSuffix )
		{
			string tipText = String.Empty; // default
			int srvCount = 0; // servent loop counter

			// iterate and append servent address text 
			foreach( Servent srv in serventList.Values )
			{	
				srvCount++; // increment count and check if max
				if( srvCount > maxLines ) // lines are exceeded
				{
					tipText += MORE_NODES + Environment.NewLine;
					break; // exit for loop
				}
				else // compile text from remote host value
				{
					tipText += linePrefix + srv.RemoteHost 
					+ lineSuffix + Environment.NewLine;
				}
			}
			return tipText; // with max servents
		}
	
		private void AddServent( Hashtable table, Servent srv )
		{
			// if servent is not already in table
			if( !table.Contains( srv.ServentGuid ) )
			{
				// add servent to specified table
				table.Add( srv.ServentGuid, srv );
			}
		}

		private void RemoveServent( Hashtable table, Servent srv )
		{
			// if servent exists in specified table
			if( table.Contains( srv.ServentGuid ) )
			{
				// remove servent from table
				table.Remove( srv.ServentGuid );						
			}
		}
	}
}
