using System;
using Buzm.Hives;
using Buzm.Schemas;
using Buzm.Utility;
using Buzm.Network;
using Buzm.Register;
using System.Drawing;
using System.Threading;
using Buzm.Network.Web;
using System.Diagnostics;
using System.Collections;
using Buzm.Network.Feeds;
using System.Windows.Forms;
using Buzm.Network.Packets;
using Buzm.Network.Sockets;
using System.Runtime.InteropServices;

namespace Buzm
{
	public class MainForm : System.Windows.Forms.Form
	{	
		// core data objects
		private User m_AppUser;
		private Registry m_Registry;
		private AppVersion m_AppVersion;
		private ArgsDictionary m_StartupArgs;

		// content management objects
		private HiveManager m_HiveManager;
		private PeerManager m_PeerManager;
		private FeedManager	m_FeedManager;
		private INetworkManager[] m_NetworkManagers;

		// interface controllers and views
		private HiveView		m_HiveView;
		private DeskController	m_DeskController;
		private HiveController	m_HiveController;
		private NetStatusPanel	m_NetStatusPanel;

		// windows shutdown vars and constants
		private bool m_IsSystemShutdown = false;
		private const int WM_ENDSESSION = 0x0016;
		private const int WM_QUERYENDSESSION = 0x0011;

		// UI control variables		
		private Icon m_AlertTrayIcon;
		private Icon m_DefaultTrayIcon;
		private bool m_MinimizedToTray;
		private FormWindowState m_LastWindowState;

		// forms designer generated variables
		private System.Windows.Forms.MainMenu m_MainMenu;
		private System.Windows.Forms.MenuItem m_FileMainMenuItem;
		private System.Windows.Forms.NotifyIcon m_TrayNotifyIcon;
		private System.Windows.Forms.Timer m_HiveUpdateTimer;
		private System.Windows.Forms.Panel m_CenterPanel;
		private System.Windows.Forms.Splitter m_VerticalSplitter;
		private System.Windows.Forms.Panel m_ContentPanel;
		private System.Windows.Forms.Panel m_ControllerPanel;
		private System.Windows.Forms.MenuItem m_ExitMainMenuItem;
		private System.Windows.Forms.ContextMenu m_TrayContextMenu;
		private System.Windows.Forms.MenuItem m_ShowTrayMenuItem;
		private System.Windows.Forms.MenuItem m_ExitTrayMenuItem;
		private System.Windows.Forms.Timer m_TrayAnimateTimer;
		private System.Windows.Forms.StatusBarPanel m_AddressStatusPanel;
		private System.Windows.Forms.StatusBar m_StatusBar;
		private System.Windows.Forms.PictureBox m_LogoPictureBox;
		private System.Windows.Forms.Panel m_HeaderBar;
		private System.Windows.Forms.Label m_HeaderBarText;
		private System.Windows.Forms.MenuItem m_EditMainMenuItem;
		private System.Windows.Forms.MenuItem m_HelpMainMenuItem;
		private System.Windows.Forms.ToolBar m_MainToolbar;
		private System.Windows.Forms.MenuItem m_LoginMainMenuItem;
		private System.Windows.Forms.MenuItem m_LoginTrayMenuItem;				
		private System.ComponentModel.IContainer components;
		private System.Windows.Forms.ImageList m_ToolbarImageList;
		private System.Windows.Forms.ToolBarButton m_PostToolbarButton;
		private System.Windows.Forms.ToolBarButton m_FeedToolbarButton;
		private System.Windows.Forms.ToolBarButton m_HiveToolbarButton;
		private System.Windows.Forms.StatusBarPanel m_GripStatusPanel;
		private System.Windows.Forms.MenuItem m_NewPostTrayMenuItem;		
		private System.Windows.Forms.MenuItem m_NewPostMainMenuItem;		
		private System.Windows.Forms.MenuItem m_NewFeedMainMenuItem;
		private System.Windows.Forms.MenuItem m_NewHiveMainMenuItem;		
		private System.Windows.Forms.MenuItem m_DeleteMainMenuItem;
		private System.Windows.Forms.MenuItem m_AboutMainMenuItem;
		private System.Windows.Forms.MenuItem m_LoginMainMenuSep;
		private System.Windows.Forms.MenuItem m_ExitMainMenuSep;
		private System.Windows.Forms.MenuItem m_LoginTrayMenuSep;
		private System.Windows.Forms.MenuItem m_ShowTrayMenuSep;
		private System.Windows.Forms.MenuItem m_NewFeedTrayMenuItem;
		private System.Windows.Forms.MenuItem m_NewHiveTrayMenuItem;
		private System.Windows.Forms.MenuItem m_ExitTrayMenuSep;
		private ToolBarButton m_MemberToolbarButton;
		private MenuItem m_NewMemberMainMenuItem;
		private MenuItem m_NewMemberTrayMenuItem;

		// default message strings for user interface components
		private const string LOGIN_STATUS = "Updating profile for {0}...";
		private const string HEADER_NOHIVE_TEXT = "Please Create a Hive to Continue";
	
		public MainForm( string[] args )
		{
			Trace.Write( "" ); // init log
			m_AppVersion = new AppVersion();

			string version = m_AppVersion.ToString(); // buzm version
			Log.Write( TraceLevel.Verbose, "Starting v" + version, "Buzm" );
			
			m_StartupArgs = new ArgsDictionary( args );	// parse command line
			m_HiveManager = new HiveManager( m_AppVersion ); // init hive store
			m_DeskController = new DeskController( m_HiveManager ); // init mvc

			InitializeComponent(); // run windows forms designer generated code
			InitializeManualComponents(); // UI initializers for custom components

			IntPtr windowHandle = this.Handle; // init handle for ISynchronizeInvoke
			RestoreApplicationState(); // must be called after window handle creation

			// initialize user registry if configured and bind to registration events
			bool registryEnabled = Config.GetBoolValue( "preferences/registry/enabled", false );
			if( registryEnabled ) m_Registry = new Registry(); // node will host a local registry
			m_HiveManager.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );			

			// initialize content network managers
			m_FeedManager = new FeedManager( this );
			m_PeerManager = new PeerManager( this, m_AppVersion );
			m_NetworkManagers = new INetworkManager[]{ m_PeerManager, m_FeedManager };

			// subscribe to necessary hive and network manager events
			m_HiveManager.HiveAdded += new ModelEventHandler( m_PeerManager.HiveManager_HiveAdded );
			m_HiveManager.FeedAdded += new ModelEventHandler( m_FeedManager.HiveManager_FeedAdded );
			m_HiveManager.HiveRemoved += new ModelEventHandler( m_PeerManager.HiveManager_HiveRemoved );
			m_HiveManager.FeedRemoved += new ModelEventHandler( m_FeedManager.HiveManager_FeedRemoved );

			// TODO: start peer manager connection thread after binding network changed event
			m_PeerManager.NetworkChanged += new NetworkChangedEventHandler( m_NetStatusPanel.PeerManager_NetworkChanged );
			m_PeerManager.NetworkChanged += new NetworkChangedEventHandler( m_PeerManager_NetworkChanged );

			// mark startup process completion to zone any setup related bugs
			Log.Write( TraceLevel.Verbose, "Startup process completed", "Buzm" );
			
			m_HiveUpdateTimer.Start(); // start processing content
			//m_TrayNotifyIcon.Visible = true; // show tray icon
		}

		#region Content Processing Methods

		/// <summary> Primary content processing timer loop </summary>
		private void HiveUpdateTimer_Tick(object sender, System.EventArgs e) 
		{
			Packet pkt;
			HiveModel hive;
			INetworkManager mgr;
			
			// iterate through registered network managers
			for( int i=0; i < m_NetworkManagers.Length; i++ )
			{
				mgr = m_NetworkManagers[i];
				if( mgr != null )
				{
					// process any queued network packets 
					while( (pkt = mgr.GetNextPacket()) != null )
					{ 
						string hiveGuid = pkt.HiveGuid;
						string packetType = pkt.GetType().Name;
						
						// content packets must contain a locally registered hive
						if( (hiveGuid != null) && m_HiveManager.HiveModels.Contains(hiveGuid) )
						{	 
							hive = (HiveModel)m_HiveManager.HiveModels[hiveGuid];
							switch( packetType ) // process packets by class sub-type
							{
								case "SynchroPacket": ProcessSynchroPacket( (SynchroPacket)pkt, hive, mgr ); break;
								case "FeedPacket": ProcessFeedPacket( (FeedPacket)pkt, hive ); break;
								default: ProcessPacket( pkt, hive, mgr.NotifyUser ); break;
							}	
						}
						else // this could be an administrative packet without a specific Hive
						{
							switch( packetType ) // process administrative packets by class sub-type
							{
								case "ArgsPacket": ProcessArgsPacket( (ArgsPacket)pkt, mgr ); break;
								case "WelcomePacket": ProcessWelcomePacket( (WelcomePacket)pkt, mgr ); break;
								case "RegisterPacket": ProcessRegisterPacket( (RegisterPacket)pkt, mgr ); break;
								default: Log.Write( "Unknown packet: " + packetType, TraceLevel.Verbose, "Buzm.HiveUpdateTimer_Tick" ); break;
							}
						} 
					}
				}
			}
		}

		private void ProcessPacket( Packet pkt, HiveModel hive, bool notifyUser )
		{
			string postXml = pkt.ToString();
			if( !String.IsNullOrEmpty( postXml ) )
			{
				ItemType postItem = ItemType.FromXml( postXml );
				if( postItem != null )
				{
					bool merged = hive.MergePost( postItem, postXml, notifyUser );
					if( merged && notifyUser && !ContainsFocus )
					{
						//TODO: start from HiveModel event
						m_TrayAnimateTimer.Start();
					}
				}
			}
		}

		private void ProcessFeedPacket( FeedPacket pkt, HiveModel hive )
		{
			// if feed is still registered with hive
			if( hive.Feeds.Contains( pkt.FeedGuid ) )
			{
				// add feed xml content to the hive
				hive.AddContent( pkt.ToString() ); 
			}
		}

		/// <summary> Manages the Hive synchronization request/response cycle. The work is done 
		/// here rather than in the Network Manager to avoid any thread safety issues </summary>
		private void ProcessSynchroPacket( SynchroPacket pkt, HiveModel hive, INetworkManager mgr )
		{
			// compare local and remote hive contents and reply with locally unique items
			if( mgr is PeerManager ) // synchro is only supported by PeerManager currently
			{
				Log.Write( pkt.ToString() + " for Hive: " + hive.Name,
				TraceLevel.Verbose, "Buzm.ProcessSynchroPacket" );

				byte[] localHiveHash; // merkle root for hive
				string[] deltaItemGuids; // items unique to hive

				if( pkt.ResponseItemGuids == null ) // sync incomplete
				{
					string[] localItemGuids = hive.GetItemGuidsWithHashes( out localHiveHash );
					deltaItemGuids = pkt.Process( localItemGuids, localHiveHash, (PeerManager)mgr );
				}
				else deltaItemGuids = pkt.ResponseItemGuids; // complete
				if( (deltaItemGuids != null) && (deltaItemGuids.Length > 0) )
				{
					// send locally unique items to request origin
					PeerEndPoint[] returnPath = pkt.GetPathToOrigin();
					SendItemsToDestination( deltaItemGuids, hive, returnPath, (PeerManager)mgr );
				}
			}
		}

		/// <summary> Searches hive for requested items and sends them to a specific destination</summary>
		private void SendItemsToDestination( string[] itemGuids, HiveModel hive, PeerEndPoint[] dest, PeerManager mgr )
		{
			Packet pkt;
			string itemXml;

			// iterate and send hive items
			for( int i=0; i < itemGuids.Length; i++ )
			{
				// retreive xml for specific item
				itemXml = hive.GetItemXml( itemGuids[i] );
				if( itemXml != null )
				{
					pkt = new Packet( itemXml, hive.Guid );
					pkt.Destination = dest; // fixed path 
					mgr.SendToDestination( pkt );
				}
			}
		}

		#endregion

		#region Content User Interface Events and Methods

		private void CreatePost()
		{
			Hashtable hives = m_HiveManager.HiveModels;
			if( User.IsAlive( m_AppUser ) && (hives.Count > 0) )
			{
				PostEditor postEditor = new PostEditor( m_AppUser, hives );				
				postEditor.Published += new EventHandler( PostEditor_Published );

				HiveModel hive = m_HiveManager.SelectedHive;
				if( hive != null ) postEditor.SelectedHive = hive;												
				postEditor.Show(); // display editor to user
			}
			else
			{
				MessageBox.Show( this, "You must have at least one active Hive to create a post.", 
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}
		}

		//TODO: refactor so that HiveModel.Updated results in SendToServents
		private void PostEditor_Published( object sender, System.EventArgs e )
		{			
			PostEditor postEditor = (PostEditor)sender;
			HiveModel hive = postEditor.SelectedHive;
			if( hive != null ) // if hive selected
			{				
				ItemType post = postEditor.PostItem;
				if( post != null ) // post created
				{
					string postXml = post.ToXml();
					if( !String.IsNullOrEmpty( postXml ) )
					{
						// create packet and send to all peers 
						Packet pkt = new Packet( postXml, hive.Guid );
						m_PeerManager.SendToServents( pkt );

						// add post to the appropriate hive
						if( m_HiveManager.HiveModels.Contains( hive.Guid ) )
						{
							hive.MergePost( post, postXml, false );
							m_HiveManager.SelectHive( hive, this );
						}
					}
				}
			}
		}

		private void m_HiveManager_PostRemoved( object sender, ModelEventArgs e )
		{
			HiveModel hive = sender as HiveModel;
			if( ( hive != null ) && ( e != null ) )
			{
				ItemType post = e.Model as ItemType;
				if( post != null ) // post found
				{
					string postXml = post.ToXml();
					if( !String.IsNullOrEmpty( postXml ) )
					{						
						Packet pkt = new Packet( postXml, hive.Guid );
						m_PeerManager.SendToServents( pkt );
					}
				}
			}
		}

		private void m_HiveManager_HiveSelected( object sender, ModelEventArgs e )
		{
			HiveModel hive = (HiveModel)e.Model;
			string prefix = "HIVE : " + hive.Name.ToUpper() + " - ";
			if( hive.StartDate == hive.EndDate ) // if one day date range
			{
				// display single long form date if hive date range only spans one day
				m_HeaderBarText.Text = prefix + hive.StartDate.ToString( "dddd MMMM dd, yyyy" );
			}
			else
			{
				m_HeaderBarText.Text = prefix + hive.StartDate.ToString( "ddd MMM dd, yyyy" )
				+ " to " + hive.EndDate.ToString( "ddd MMM dd, yyyy" );
			}
		}

		private void m_HiveManager_HiveUpdated( object sender, ModelEventArgs e )
		{
			HiveModel hive = (HiveModel)e.Model;
			if( hive == m_HiveManager.SelectedHive )
			{
				// same m_HeaderBarText as selected
				m_HiveManager_HiveSelected( sender, e );
			}
		}

		private void m_HiveManager_HiveRemoved( object sender, ModelEventArgs e )
		{
			// if the user has no hives remaining
			if( m_HiveManager.HiveModels.Count == 0 )
			{
				m_HiveView.ClearView(); // set default views
				m_HeaderBarText.Text = HEADER_NOHIVE_TEXT;
			}
		}

		// Windows API call to flash start bar title
		[DllImport("user32", EntryPoint="FlashWindow")]
		private static extern long FlashWindow( IntPtr hWnd, bool bInvert );

		private void TrayAnimateTimer_Tick(object sender, System.EventArgs e)
		{
			if( !ContainsFocus ) // form does not have input focus
			{
				// flash start bar title and system tray icon on each tick
				if( !m_MinimizedToTray ){ FlashWindow( this.Handle, true ); }				
				
				if( m_TrayNotifyIcon.Icon == m_DefaultTrayIcon ) SetTrayIcon( m_AlertTrayIcon );				
				else SetTrayIcon( m_DefaultTrayIcon ); // switch alert icon to default

			}
			else // the form has been focused so stop alerting the user
			{
				m_TrayAnimateTimer.Stop(); // stop animation timer
				SetTrayIcon( m_DefaultTrayIcon ); // reset icon
			}
		}

		#endregion	

		#region User Login and Registry Events and Methods

		private DialogResult ProcessLogin() { return ProcessLogin( UserLoginState.Manual ); }
		private DialogResult ProcessLogin( UserLoginState targetState )
		{
			if( ( m_AppUser != null ) && ( targetState == UserLoginState.Auto ) 
			 && ( m_Registry == null ) ) // auto login if no local registry
			{
				// ensure that user has not already logged in
				if( m_AppUser.LoginState < UserLoginState.Auto )
				{
					m_AppUser.RememberLogin = true;
					m_AppUser.LoginState = UserLoginState.Auto;

					RegistryEventArgs args = new RegistryEventArgs( m_AppUser, RegistryAction.LoginUser, String.Empty );
					UserEditor_UserActivated( this, args ); // simulate login
				}
				return DialogResult.OK; // user should now be logged in
			}
			else // user must be authenticated by the registry
			{
				UserEditor userEditor = new UserEditor( RegistryAction.LoginUser, m_AppUser, targetState );
				userEditor.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );
				userEditor.UserActivated += new RegistryEventHandler( UserEditor_UserActivated );

				// if the login should be processed automatically
				if( targetState == UserLoginState.Silent )
				{
					userEditor.Show( this ); // non-modal form
					userEditor.AcceptButton.PerformClick();
				}
				else return userEditor.ShowDialog( this );
			}
			return DialogResult.None; // login pending
		}

		private void m_PeerManager_NetworkChanged( PeerManager mgr, Servent srv, bool async )
		{
			// if a connection is made and we are setup to use a remote registry
			if( ( srv.Status == ServentStatus.Connected ) && ( m_Registry == null ) )
			{
				// if the global user exists and has not yet been registry authenticated
				if( ( m_AppUser != null ) && ( m_AppUser.LoginState < UserLoginState.Silent ) )
				{
					if( Application.OpenForms["UserEditor"] == null ) // prevent repeats
					{
						// TODO: should only run if servent provides a path to registry
						ProcessLogin( UserLoginState.Silent ); // background login
					}
				}
			}
		}

		private void ProcessInvite( string inviteFile )
		{
			if( User.IsAlive( m_AppUser ) && ( m_HiveManager != null ) ) // if user logged in
			{
				InviteActor inviteActor = new InviteActor( inviteFile, m_AppUser, m_HiveManager );
				inviteActor.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );
				inviteActor.ShowDialog( this ); // process invite and display progress to user
			}
		}

		private void RegistryEditor_RegistryRequest( object sender, RegistryEventArgs e )
		{						
			// if this is a local registry or we are connected to the network
			if( (m_Registry != null) || (m_PeerManager.ConnectionCount() > 0) )
			{
				RegisterPacket pkt = new RegisterPacket( e.User.ToXmlString(), (int)e.Action );
				pkt.ActionGuid = e.ActionGuid; // bind request guids so response can be matched
				ProcessRegisterPacket( pkt, m_PeerManager ); // local or remote registry used
			}
			// signal a network error - client has the option of retrying the registry action 
			else ProcessRegistryResult( e.User, RegistryResult.NetworkError, "", e.ActionGuid );
		}

		/// <summary> Manages the user account update request/response cycle. The work is done 
		/// here rather than in the Network Manager to avoid any thread safety issues </summary>
		private void ProcessRegisterPacket( RegisterPacket pkt, INetworkManager mgr )
		{
			if( mgr is PeerManager ) // PeerManager needed for registry
			{	
				string resultMsg = pkt.ResultMessage; // unpack result
				RegistryResult result = (RegistryResult)pkt.ResultCode;

				if( result == RegistryResult.None ) // unprocessed request
				{
					RegistryAction action = (RegistryAction)pkt.ActionCode;
					if( m_Registry != null ) // if there is a local registry
					{						
						SafeXmlDoc xmlDoc = new SafeXmlDoc( pkt.ToString() );
						User user = new User( xmlDoc ); // new user from packet xml
						result = m_Registry.ProcessUserAction( ref user, action, out resultMsg );

						// in the rare case that the packet is of local origin handle it immediately
						if( pkt.Origin.Length == 0 ) ProcessRegistryResult( user, result, resultMsg, pkt.ActionGuid );
						else 
						{	// otherwise create new RegisterPacket with the result and send it to the requesting peer
							RegisterPacket resultPkt = new RegisterPacket( user.ToXmlString(), (int)result, resultMsg );
							resultPkt.ActionGuid = pkt.ActionGuid; // bind guids so that result can be matched
							resultPkt.Destination = pkt.GetPathToOrigin(); // fixed return path to origin
							((PeerManager)mgr).SendToDestination( resultPkt );
						}					
					}
					else ((PeerManager)mgr).SendToServents( pkt ); // no local registry so forward packet to remote one 
				}
				else // this packet is in response to an earlier request
				{
					SafeXmlDoc xmlDoc = new SafeXmlDoc( pkt.ToString() );
					User user = new User( xmlDoc ); // create user from xmlDoc
					ProcessRegistryResult( user, result, resultMsg, pkt.ActionGuid );
				}
			}
		}

		private void ProcessRegistryResult( User user, RegistryResult result, string resultMsg, string actionGuid )
		{
			RegistryEventArgs e = new RegistryEventArgs( user, result, resultMsg, actionGuid );
			RegistryEditor.OnRegistryResponse( this, e ); // send result to active registry editors
		}
		
		private void UserEditor_UserActivated( object sender, RegistryEventArgs e )
		{
			if( ( e != null ) && User.IsAlive( e.User ) )
			{
				string newUserLogin = e.User.Login;
				if( !String.IsNullOrEmpty( newUserLogin ) )
				{
					if( !( User.IsAlive( m_AppUser )
					 && ( m_AppUser.Login == newUserLogin )
					 && ( m_AppUser.LoginState > e.User.LoginState ) ) )
					{
						this.Cursor = Cursors.WaitCursor; // may take a while
						m_AddressStatusPanel.Text = String.Format( LOGIN_STATUS, newUserLogin );

						m_AppUser = e.User; // save new or upgraded user object
						m_HiveManager.HiveUser = m_AppUser; // load user hives

						m_HiveController.HiveUser = m_AppUser; // show in tree
						EnablePostingInterface(); // allow user to edit posts
						
						if( m_AppUser.LoginState > UserLoginState.Auto )
						{
							EnableProfileInterface(); // registry online
						}
						if( m_HiveManager.HiveModels.Count == 0 )
						{
							m_HiveView.ClearView(); // set default view
							m_HeaderBarText.Text = HEADER_NOHIVE_TEXT;
						}
						m_AddressStatusPanel.Text = String.Empty;
						this.Cursor = Cursors.Default;
					}
				}				
			}			
		}

		private void EnablePostingInterface()
		{
			m_PostToolbarButton.Enabled = true;
			m_NewPostMainMenuItem.Enabled = true;
			m_NewPostTrayMenuItem.Enabled = true;
		}

		private void EnableProfileInterface()
		{			
			// enable buttons in main toolbar			
			m_FeedToolbarButton.Enabled = true;
			m_HiveToolbarButton.Enabled = true;
			m_MemberToolbarButton.Enabled = true;

			// enable items in buzm main menu
			m_DeleteMainMenuItem.Enabled = true;			
			m_NewFeedMainMenuItem.Enabled = true;
			m_NewHiveMainMenuItem.Enabled = true;			
			m_NewMemberMainMenuItem.Enabled = true;

			// enable items in tray context menu			
			m_NewFeedTrayMenuItem.Enabled = true;
			m_NewHiveTrayMenuItem.Enabled = true;
			m_NewMemberTrayMenuItem.Enabled = true;
		}

		private void ProcessWelcomePacket( WelcomePacket pkt, INetworkManager mgr )
		{ 
			string message = pkt.Message; // human readable text
			if( ( message != null ) && ( message != String.Empty ) )
			{
				if( MessageBox.Show( message, "Buzm Upgrade Notice", // ask user to upgrade
					MessageBoxButtons.YesNo, MessageBoxIcon.Information ) == DialogResult.Yes )
				{
					string link = pkt.Link; // version upgrade url
					if( ( link != null ) && ( link != String.Empty ) )
					{
						// since method can start any program check if link is safe
						if( link.StartsWith( "http://" ) || link.StartsWith( "ftp://" ) )
						{
							// open default browser to download upgrade
							System.Diagnostics.Process.Start( link );
						}
					}
				}
			}
		}

		#endregion

		#region Menu Events and Supporting Methods

		private void m_TrayNotifyIcon_DoubleClick( object sender, System.EventArgs e ) 
		{
			RestoreFromTray(); // display application window
		}

		private void m_HeaderBarText_Click(object sender, System.EventArgs e)
		{
			if( User.IsAlive( m_AppUser ) ) m_HiveView.GoHome();
			else ProcessLogin( UserLoginState.Auto );
		}		

		private void m_MainToolbar_ButtonClick( object sender, System.Windows.Forms.ToolBarButtonClickEventArgs e )
		{
			// check Button property to determine which button was clicked
			switch( m_MainToolbar.Buttons.IndexOf( e.Button ) )
			{
				case 0: // post button
					CreatePost();
					break; 
				case 1: // feed button
					m_HiveManager.NewFeed_Click( this, new EventArgs() );
					break;
				case 2: // hive button
					m_HiveManager.NewHive_Click( this, new EventArgs() );
					break;
				case 3: // member button
					m_HiveManager.NewMember_Click( this, new EventArgs() );
					break;
			}
		}

		private void NewPostMenuItem_Click(object sender, System.EventArgs e)
		{
			CreatePost();
		}

		private void LoginMenuItem_Click(object sender, System.EventArgs e)
		{
			ProcessLogin(); // Show the main login and registration window
		}

		private void show_MenuItemClick( object sender, System.EventArgs e )
		{
			m_TrayNotifyIcon_DoubleClick( sender, e );
		}

		private void exit_MenuItemClick( object sender, System.EventArgs e )
		{
			Log.Write( "Buzm shutdown by user exit menu click", 
			TraceLevel.Verbose, "Buzm.exit_MenuItemClick" );

			Visible = false;
			CleanupAndExit();
		}

		private void AboutMenuItem_Click( object sender, System.EventArgs e )
		{
			About aboutBuzm = new About( m_AppVersion ); // create form
			aboutBuzm.ShowDialog( this ); // show form as modal window
		}

		#endregion

		#region Form Events and Supporting Methods

		private void MainForm_Load(object sender, System.EventArgs e)
		{
			// the proper splitter position can only be set after the form has been loaded
			m_VerticalSplitter.SplitPosition = Config.Settings.WindowSplitter; // load saved position
			m_VerticalSplitter.SplitPosition = m_HiveController.SnapToWidth( m_VerticalSplitter.SplitPosition );

			if( m_StartupArgs != null ) // if arguments were parsed from command line
			{
				SetupUserEnvironment( m_StartupArgs ); // process login and invite
				if( m_StartupArgs.ContainsKey( "test" ) ) NUnitHarness.RunAllTests();
			}
		}

		private void SetupUserEnvironment( ArgsDictionary args )
		{	
			RestoreFromTray(); // force the application window to display
			if( args.ContainsKey( "invite" ) ) // if invite argument exists
			{				
				// check if user is logged in, otherwise wait till the user logs in
				if( User.IsAlive( m_AppUser ) || ( ProcessLogin( UserLoginState.Auto ) == DialogResult.OK ) )
				{					
					ProcessInvite( args["invite"] ); // process invitation file
				}
			}
			else if( !args.RemoteArgs ) ProcessLogin( UserLoginState.Auto );
		}

		//TODO: prevent multiple args packets from processing at same time
		private void ProcessArgsPacket( ArgsPacket pkt, INetworkManager mgr )
		{ 
			ArgsDictionary args = new ArgsDictionary( pkt.Arguments );
			args.RemoteArgs = true; // args came from another instance
			SetupUserEnvironment( args ); // run args from new instance
		}

		private void MainForm_Activated(object sender, System.EventArgs e)
		{			
			if( !m_MinimizedToTray ) m_TrayAnimateTimer.Stop();
			else { Hide(); /* Should only be visible in tray */ }
			SetTrayIcon( m_DefaultTrayIcon ); // set default icon
		}

		private void MinimizeToTray()
		{	
			m_MinimizedToTray = true;
			m_LastWindowState = WindowState;
			WindowState = FormWindowState.Minimized;			
			Hide(); // hide the main form
		}

		private void RestoreFromTray()
		{
			Show(); // renders form 
			m_MinimizedToTray = false;
			WindowState = m_LastWindowState;
			TopMost = true; TopMost = false;
			Focus(); // forces form to focus 
		}

		private void SaveApplicationState()
		{
			if( this.WindowState != FormWindowState.Minimized )
				Config.Settings.WindowState = this.WindowState;

			if( this.WindowState == FormWindowState.Normal )
				 Config.Settings.WindowBounds = this.Bounds;
			else Config.Settings.WindowBounds = this.RestoreBounds;

			Config.Settings.WindowSplitter = m_VerticalSplitter.SplitPosition;

			if( User.IsAlive( m_AppUser ) && m_AppUser.RememberLogin )
			{
				m_HiveManager.SynchronizeHives( m_AppUser, false );
				m_AppUser.SaveToConfig(); // serialize user xml
			}
			Config.Settings.Save(); // write to disk
		}

		private void RestoreApplicationState()
		{
			// deserialize config user info
			m_AppUser = User.LoadFromConfig();

			Rectangle? windowBounds = Config.Settings.WindowBounds;
			if( windowBounds != null )
			{
				this.StartPosition = FormStartPosition.Manual;
				this.Bounds = (Rectangle)windowBounds;
			}
		}

		private void SetTrayIcon( Icon trayIcon )
		{
			// if tray icon is currently showing set it to specified value 
			if( m_TrayNotifyIcon.Visible ) m_TrayNotifyIcon.Icon = trayIcon;			
		}

		private void VerticalSplitter_SplitterMoving( object sender, SplitterEventArgs e)
		{
			// Snap the the splitter to the appropriate position
			e.SplitX = ( m_HiveController.SnapToWidth( e.SplitX ) );
		}

		private void VerticalSplitter_SplitterMoved( object sender, SplitterEventArgs e)
		{
			// Redraw clean view
			m_HiveController.Refresh();
		}	

		private void MainForm_KeyDown( object sender, KeyEventArgs e )
		{
			if( User.IsAlive( m_AppUser ) && // only if user is logged in
				((e.Control) && (e.KeyCode == Keys.N)) ) CreatePost();
		}		

		// This method is required to distinguish system
		// shutdowns from user initiated application quits.
		protected override void WndProc( ref Message m )
		{
			switch( m.Msg )
			{
				case WM_ENDSESSION: CleanupAndExit(); break;
				case WM_QUERYENDSESSION: m_IsSystemShutdown = true; break;				
			}
			base.WndProc( ref m );
		}

		private void MainForm_Closing( object sender, System.ComponentModel.CancelEventArgs e ) 
		{
			// The flag is cleared in case another application
			// decides to Cancel the shutdown event later on.
			if( m_IsSystemShutdown ) m_IsSystemShutdown = false;
			else
			{
				MinimizeToTray();
				e.Cancel = true;			
			}
		}

		private void CleanupAndExit() 
		{
			Log.Write( "Buzm cleaning up resources...", 
			TraceLevel.Verbose, "Buzm.CleanupAndExit" );

			SaveApplicationState(); // save state to disk

			INetworkManager mgr; // close network managers
			for( int i=0; i < m_NetworkManagers.Length; i++ )
			{
				mgr = m_NetworkManagers[i];
				if( mgr != null ) mgr.Close();
			}

			m_TrayNotifyIcon.Visible = false;
			m_TrayNotifyIcon.Dispose();

			Log.Write( "Normal shutdown completed.", 
			TraceLevel.Off, "Buzm.CleanupAndExit" );
			
			// End the process
			Application.Exit();
		}

		/// <summary> Clean up any resources </summary>
		protected override void Dispose( bool disposing ) 
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#endregion
		
		#region Windows Form Designer generated code and UI Initializers
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
			this.m_MainMenu = new System.Windows.Forms.MainMenu(this.components);
			this.m_FileMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_LoginMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_LoginMainMenuSep = new System.Windows.Forms.MenuItem();
			this.m_NewPostMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_NewFeedMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_NewHiveMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_ExitMainMenuSep = new System.Windows.Forms.MenuItem();
			this.m_ExitMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_EditMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_DeleteMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_HelpMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_AboutMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_TrayNotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
			this.m_TrayContextMenu = new System.Windows.Forms.ContextMenu();
			this.m_ShowTrayMenuItem = new System.Windows.Forms.MenuItem();
			this.m_ShowTrayMenuSep = new System.Windows.Forms.MenuItem();
			this.m_NewPostTrayMenuItem = new System.Windows.Forms.MenuItem();
			this.m_NewFeedTrayMenuItem = new System.Windows.Forms.MenuItem();
			this.m_NewHiveTrayMenuItem = new System.Windows.Forms.MenuItem();
			this.m_LoginTrayMenuSep = new System.Windows.Forms.MenuItem();
			this.m_LoginTrayMenuItem = new System.Windows.Forms.MenuItem();
			this.m_ExitTrayMenuSep = new System.Windows.Forms.MenuItem();
			this.m_ExitTrayMenuItem = new System.Windows.Forms.MenuItem();
			this.m_HiveUpdateTimer = new System.Windows.Forms.Timer(this.components);
			this.m_CenterPanel = new System.Windows.Forms.Panel();
			this.m_ContentPanel = new System.Windows.Forms.Panel();
			this.m_VerticalSplitter = new System.Windows.Forms.Splitter();
			this.m_ControllerPanel = new System.Windows.Forms.Panel();
			this.m_HeaderBar = new System.Windows.Forms.Panel();
			this.m_HeaderBarText = new System.Windows.Forms.Label();
			this.m_TrayAnimateTimer = new System.Windows.Forms.Timer(this.components);
			this.m_MainToolbar = new System.Windows.Forms.ToolBar();
			this.m_PostToolbarButton = new System.Windows.Forms.ToolBarButton();
			this.m_FeedToolbarButton = new System.Windows.Forms.ToolBarButton();
			this.m_HiveToolbarButton = new System.Windows.Forms.ToolBarButton();
			this.m_MemberToolbarButton = new System.Windows.Forms.ToolBarButton();
			this.m_ToolbarImageList = new System.Windows.Forms.ImageList(this.components);
			this.m_StatusBar = new System.Windows.Forms.StatusBar();
			this.m_AddressStatusPanel = new System.Windows.Forms.StatusBarPanel();
			this.m_GripStatusPanel = new System.Windows.Forms.StatusBarPanel();
			this.m_LogoPictureBox = new System.Windows.Forms.PictureBox();
			this.m_NewMemberMainMenuItem = new System.Windows.Forms.MenuItem();
			this.m_NewMemberTrayMenuItem = new System.Windows.Forms.MenuItem();
			this.m_CenterPanel.SuspendLayout();
			this.m_HeaderBar.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.m_AddressStatusPanel)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_GripStatusPanel)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_LogoPictureBox)).BeginInit();
			this.SuspendLayout();
			// 
			// m_MainMenu
			// 
			this.m_MainMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.m_FileMainMenuItem,
            this.m_EditMainMenuItem,
            this.m_HelpMainMenuItem});
			this.m_MainMenu.RightToLeft = System.Windows.Forms.RightToLeft.No;
			// 
			// m_FileMainMenuItem
			// 
			this.m_FileMainMenuItem.Index = 0;
			this.m_FileMainMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.m_LoginMainMenuItem,
            this.m_LoginMainMenuSep,
            this.m_NewPostMainMenuItem,
            this.m_NewFeedMainMenuItem,
            this.m_NewHiveMainMenuItem,
            this.m_NewMemberMainMenuItem,
            this.m_ExitMainMenuSep,
            this.m_ExitMainMenuItem});
			this.m_FileMainMenuItem.Text = "&File";
			// 
			// m_LoginMainMenuItem
			// 
			this.m_LoginMainMenuItem.Index = 0;
			this.m_LoginMainMenuItem.Text = "&Login...";
			this.m_LoginMainMenuItem.Click += new System.EventHandler(this.LoginMenuItem_Click);
			// 
			// m_LoginMainMenuSep
			// 
			this.m_LoginMainMenuSep.Index = 1;
			this.m_LoginMainMenuSep.Text = "-";
			// 
			// m_NewPostMainMenuItem
			// 
			this.m_NewPostMainMenuItem.Enabled = false;
			this.m_NewPostMainMenuItem.Index = 2;
			this.m_NewPostMainMenuItem.Shortcut = System.Windows.Forms.Shortcut.CtrlN;
			this.m_NewPostMainMenuItem.Text = "&New Post";
			this.m_NewPostMainMenuItem.Click += new System.EventHandler(this.NewPostMenuItem_Click);
			// 
			// m_NewFeedMainMenuItem
			// 
			this.m_NewFeedMainMenuItem.Enabled = false;
			this.m_NewFeedMainMenuItem.Index = 3;
			this.m_NewFeedMainMenuItem.Shortcut = System.Windows.Forms.Shortcut.CtrlD;
			this.m_NewFeedMainMenuItem.Text = "&Add Feed";
			// 
			// m_NewHiveMainMenuItem
			// 
			this.m_NewHiveMainMenuItem.Enabled = false;
			this.m_NewHiveMainMenuItem.Index = 4;
			this.m_NewHiveMainMenuItem.Shortcut = System.Windows.Forms.Shortcut.CtrlH;
			this.m_NewHiveMainMenuItem.Text = "&Create Hive";
			// 
			// m_ExitMainMenuSep
			// 
			this.m_ExitMainMenuSep.Index = 6;
			this.m_ExitMainMenuSep.Text = "-";
			// 
			// m_ExitMainMenuItem
			// 
			this.m_ExitMainMenuItem.Index = 7;
			this.m_ExitMainMenuItem.Text = "&Exit";
			this.m_ExitMainMenuItem.Click += new System.EventHandler(this.exit_MenuItemClick);
			// 
			// m_EditMainMenuItem
			// 
			this.m_EditMainMenuItem.Index = 1;
			this.m_EditMainMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.m_DeleteMainMenuItem});
			this.m_EditMainMenuItem.Text = "&Edit";
			// 
			// m_DeleteMainMenuItem
			// 
			this.m_DeleteMainMenuItem.Enabled = false;
			this.m_DeleteMainMenuItem.Index = 0;
			this.m_DeleteMainMenuItem.Shortcut = System.Windows.Forms.Shortcut.Del;
			this.m_DeleteMainMenuItem.Text = "&Delete";
			// 
			// m_HelpMainMenuItem
			// 
			this.m_HelpMainMenuItem.Index = 2;
			this.m_HelpMainMenuItem.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.m_AboutMainMenuItem});
			this.m_HelpMainMenuItem.Text = "&Help";
			// 
			// m_AboutMainMenuItem
			// 
			this.m_AboutMainMenuItem.Index = 0;
			this.m_AboutMainMenuItem.Text = "&About Buzm";
			this.m_AboutMainMenuItem.Click += new System.EventHandler(this.AboutMenuItem_Click);
			// 
			// m_TrayNotifyIcon
			// 
			this.m_TrayNotifyIcon.ContextMenu = this.m_TrayContextMenu;
			this.m_TrayNotifyIcon.Text = "The Buzm Network";
			this.m_TrayNotifyIcon.DoubleClick += new System.EventHandler(this.m_TrayNotifyIcon_DoubleClick);
			// 
			// m_TrayContextMenu
			// 
			this.m_TrayContextMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.m_ShowTrayMenuItem,
            this.m_ShowTrayMenuSep,
            this.m_NewPostTrayMenuItem,
            this.m_NewFeedTrayMenuItem,
            this.m_NewHiveTrayMenuItem,
            this.m_NewMemberTrayMenuItem,
            this.m_LoginTrayMenuSep,
            this.m_LoginTrayMenuItem,
            this.m_ExitTrayMenuSep,
            this.m_ExitTrayMenuItem});
			// 
			// m_ShowTrayMenuItem
			// 
			this.m_ShowTrayMenuItem.Index = 0;
			this.m_ShowTrayMenuItem.Text = "Show Buzm";
			this.m_ShowTrayMenuItem.Click += new System.EventHandler(this.show_MenuItemClick);
			// 
			// m_ShowTrayMenuSep
			// 
			this.m_ShowTrayMenuSep.Index = 1;
			this.m_ShowTrayMenuSep.Text = "-";
			// 
			// m_NewPostTrayMenuItem
			// 
			this.m_NewPostTrayMenuItem.Enabled = false;
			this.m_NewPostTrayMenuItem.Index = 2;
			this.m_NewPostTrayMenuItem.Text = "New Post";
			this.m_NewPostTrayMenuItem.Click += new System.EventHandler(this.NewPostMenuItem_Click);
			// 
			// m_NewFeedTrayMenuItem
			// 
			this.m_NewFeedTrayMenuItem.Enabled = false;
			this.m_NewFeedTrayMenuItem.Index = 3;
			this.m_NewFeedTrayMenuItem.Text = "Add Feed";
			// 
			// m_NewHiveTrayMenuItem
			// 
			this.m_NewHiveTrayMenuItem.Enabled = false;
			this.m_NewHiveTrayMenuItem.Index = 4;
			this.m_NewHiveTrayMenuItem.Text = "Create Hive";
			// 
			// m_LoginTrayMenuSep
			// 
			this.m_LoginTrayMenuSep.Index = 6;
			this.m_LoginTrayMenuSep.Text = "-";
			// 
			// m_LoginTrayMenuItem
			// 
			this.m_LoginTrayMenuItem.Index = 7;
			this.m_LoginTrayMenuItem.Text = "Login...";
			this.m_LoginTrayMenuItem.Click += new System.EventHandler(this.LoginMenuItem_Click);
			// 
			// m_ExitTrayMenuSep
			// 
			this.m_ExitTrayMenuSep.Index = 8;
			this.m_ExitTrayMenuSep.Text = "-";
			// 
			// m_ExitTrayMenuItem
			// 
			this.m_ExitTrayMenuItem.Index = 9;
			this.m_ExitTrayMenuItem.Text = "Exit";
			this.m_ExitTrayMenuItem.Click += new System.EventHandler(this.exit_MenuItemClick);
			// 
			// m_HiveUpdateTimer
			// 
			this.m_HiveUpdateTimer.Interval = 1000;
			this.m_HiveUpdateTimer.Tick += new System.EventHandler(this.HiveUpdateTimer_Tick);
			// 
			// m_CenterPanel
			// 
			this.m_CenterPanel.BackColor = System.Drawing.SystemColors.Control;
			this.m_CenterPanel.Controls.Add(this.m_ContentPanel);
			this.m_CenterPanel.Controls.Add(this.m_VerticalSplitter);
			this.m_CenterPanel.Controls.Add(this.m_ControllerPanel);
			this.m_CenterPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_CenterPanel.Location = new System.Drawing.Point(2, 47);
			this.m_CenterPanel.Name = "m_CenterPanel";
			this.m_CenterPanel.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
			this.m_CenterPanel.Size = new System.Drawing.Size(980, 602);
			this.m_CenterPanel.TabIndex = 12;
			// 
			// m_ContentPanel
			// 
			this.m_ContentPanel.BackColor = System.Drawing.SystemColors.Control;
			this.m_ContentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_ContentPanel.Location = new System.Drawing.Point(186, 2);
			this.m_ContentPanel.Name = "m_ContentPanel";
			this.m_ContentPanel.Size = new System.Drawing.Size(794, 600);
			this.m_ContentPanel.TabIndex = 2;
			// 
			// m_VerticalSplitter
			// 
			this.m_VerticalSplitter.Location = new System.Drawing.Point(184, 2);
			this.m_VerticalSplitter.Name = "m_VerticalSplitter";
			this.m_VerticalSplitter.Size = new System.Drawing.Size(2, 600);
			this.m_VerticalSplitter.TabIndex = 1;
			this.m_VerticalSplitter.TabStop = false;
			this.m_VerticalSplitter.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.VerticalSplitter_SplitterMoved);
			this.m_VerticalSplitter.SplitterMoving += new System.Windows.Forms.SplitterEventHandler(this.VerticalSplitter_SplitterMoving);
			// 
			// m_ControllerPanel
			// 
			this.m_ControllerPanel.BackColor = System.Drawing.SystemColors.Control;
			this.m_ControllerPanel.Dock = System.Windows.Forms.DockStyle.Left;
			this.m_ControllerPanel.Location = new System.Drawing.Point(0, 2);
			this.m_ControllerPanel.Name = "m_ControllerPanel";
			this.m_ControllerPanel.Size = new System.Drawing.Size(184, 600);
			this.m_ControllerPanel.TabIndex = 0;
			// 
			// m_HeaderBar
			// 
			this.m_HeaderBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(153)))), ((int)(((byte)(153)))), ((int)(((byte)(153)))));
			this.m_HeaderBar.Controls.Add(this.m_HeaderBarText);
			this.m_HeaderBar.Dock = System.Windows.Forms.DockStyle.Top;
			this.m_HeaderBar.ForeColor = System.Drawing.SystemColors.Control;
			this.m_HeaderBar.Location = new System.Drawing.Point(2, 27);
			this.m_HeaderBar.Name = "m_HeaderBar";
			this.m_HeaderBar.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
			this.m_HeaderBar.Size = new System.Drawing.Size(980, 20);
			this.m_HeaderBar.TabIndex = 0;
			// 
			// m_HeaderBarText
			// 
			this.m_HeaderBarText.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(153)))), ((int)(((byte)(153)))), ((int)(((byte)(153)))));
			this.m_HeaderBarText.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_HeaderBarText.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_HeaderBarText.ForeColor = System.Drawing.SystemColors.ControlLightLight;
			this.m_HeaderBarText.Location = new System.Drawing.Point(6, 0);
			this.m_HeaderBarText.Name = "m_HeaderBarText";
			this.m_HeaderBarText.Size = new System.Drawing.Size(974, 20);
			this.m_HeaderBarText.TabIndex = 0;
			this.m_HeaderBarText.Text = "Please Login or Register to Begin";
			this.m_HeaderBarText.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.m_HeaderBarText.Click += new System.EventHandler(this.m_HeaderBarText_Click);
			// 
			// m_TrayAnimateTimer
			// 
			this.m_TrayAnimateTimer.Interval = 1000;
			this.m_TrayAnimateTimer.Tick += new System.EventHandler(this.TrayAnimateTimer_Tick);
			// 
			// m_MainToolbar
			// 
			this.m_MainToolbar.Appearance = System.Windows.Forms.ToolBarAppearance.Flat;
			this.m_MainToolbar.AutoSize = false;
			this.m_MainToolbar.Buttons.AddRange(new System.Windows.Forms.ToolBarButton[] {
            this.m_PostToolbarButton,
            this.m_FeedToolbarButton,
            this.m_HiveToolbarButton,
            this.m_MemberToolbarButton});
			this.m_MainToolbar.DropDownArrows = true;
			this.m_MainToolbar.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_MainToolbar.ImageList = this.m_ToolbarImageList;
			this.m_MainToolbar.Location = new System.Drawing.Point(2, 2);
			this.m_MainToolbar.Name = "m_MainToolbar";
			this.m_MainToolbar.ShowToolTips = true;
			this.m_MainToolbar.Size = new System.Drawing.Size(980, 25);
			this.m_MainToolbar.TabIndex = 14;
			this.m_MainToolbar.TextAlign = System.Windows.Forms.ToolBarTextAlign.Right;
			this.m_MainToolbar.Wrappable = false;
			this.m_MainToolbar.ButtonClick += new System.Windows.Forms.ToolBarButtonClickEventHandler(this.m_MainToolbar_ButtonClick);
			// 
			// m_PostToolbarButton
			// 
			this.m_PostToolbarButton.Enabled = false;
			this.m_PostToolbarButton.ImageIndex = 0;
			this.m_PostToolbarButton.Name = "m_PostToolbarButton";
			this.m_PostToolbarButton.Text = " New Post";
			this.m_PostToolbarButton.ToolTipText = "Post a message to any Hive";
			// 
			// m_FeedToolbarButton
			// 
			this.m_FeedToolbarButton.Enabled = false;
			this.m_FeedToolbarButton.ImageIndex = 1;
			this.m_FeedToolbarButton.Name = "m_FeedToolbarButton";
			this.m_FeedToolbarButton.Text = " Add Feed";
			this.m_FeedToolbarButton.ToolTipText = "Add an RSS feed to your Hive";
			// 
			// m_HiveToolbarButton
			// 
			this.m_HiveToolbarButton.Enabled = false;
			this.m_HiveToolbarButton.ImageIndex = 2;
			this.m_HiveToolbarButton.Name = "m_HiveToolbarButton";
			this.m_HiveToolbarButton.Text = " Create Hive";
			this.m_HiveToolbarButton.ToolTipText = "Create a new Hive";
			// 
			// m_MemberToolbarButton
			// 
			this.m_MemberToolbarButton.Enabled = false;
			this.m_MemberToolbarButton.ImageIndex = 4;
			this.m_MemberToolbarButton.Name = "m_MemberToolbarButton";
			this.m_MemberToolbarButton.Text = " Send Invite";
			this.m_MemberToolbarButton.ToolTipText = "Invite friends to join your Hive";
			// 
			// m_ToolbarImageList
			// 
			this.m_ToolbarImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("m_ToolbarImageList.ImageStream")));
			this.m_ToolbarImageList.TransparentColor = System.Drawing.Color.Transparent;
			this.m_ToolbarImageList.Images.SetKeyName(0, "");
			this.m_ToolbarImageList.Images.SetKeyName(1, "");
			this.m_ToolbarImageList.Images.SetKeyName(2, "");
			this.m_ToolbarImageList.Images.SetKeyName(3, "");
			this.m_ToolbarImageList.Images.SetKeyName(4, "");
			// 
			// m_StatusBar
			// 
			this.m_StatusBar.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_StatusBar.Location = new System.Drawing.Point(2, 649);
			this.m_StatusBar.Name = "m_StatusBar";
			this.m_StatusBar.Panels.AddRange(new System.Windows.Forms.StatusBarPanel[] {
            this.m_AddressStatusPanel,
            this.m_GripStatusPanel});
			this.m_StatusBar.ShowPanels = true;
			this.m_StatusBar.Size = new System.Drawing.Size(980, 22);
			this.m_StatusBar.TabIndex = 15;
			// 
			// m_AddressStatusPanel
			// 
			this.m_AddressStatusPanel.AutoSize = System.Windows.Forms.StatusBarPanelAutoSize.Spring;
			this.m_AddressStatusPanel.Name = "m_AddressStatusPanel";
			this.m_AddressStatusPanel.Width = 959;
			// 
			// m_GripStatusPanel
			// 
			this.m_GripStatusPanel.BorderStyle = System.Windows.Forms.StatusBarPanelBorderStyle.None;
			this.m_GripStatusPanel.MinWidth = 2;
			this.m_GripStatusPanel.Name = "m_GripStatusPanel";
			this.m_GripStatusPanel.Width = 4;
			// 
			// m_LogoPictureBox
			// 
			this.m_LogoPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.m_LogoPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_LogoPictureBox.Image")));
			this.m_LogoPictureBox.Location = new System.Drawing.Point(587, 5);
			this.m_LogoPictureBox.Name = "m_LogoPictureBox";
			this.m_LogoPictureBox.Size = new System.Drawing.Size(395, 42);
			this.m_LogoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_LogoPictureBox.TabIndex = 16;
			this.m_LogoPictureBox.TabStop = false;
			// 
			// m_NewMemberMainMenuItem
			// 
			this.m_NewMemberMainMenuItem.Enabled = false;
			this.m_NewMemberMainMenuItem.Index = 5;
			this.m_NewMemberMainMenuItem.Shortcut = System.Windows.Forms.Shortcut.CtrlM;
			this.m_NewMemberMainMenuItem.Text = "&Send Invite";
			// 
			// m_NewMemberTrayMenuItem
			// 
			this.m_NewMemberTrayMenuItem.Enabled = false;
			this.m_NewMemberTrayMenuItem.Index = 5;
			this.m_NewMemberTrayMenuItem.Text = "Send Invite";
			// 
			// MainForm
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(4, 11);
			this.ClientSize = new System.Drawing.Size(984, 673);
			this.Controls.Add(this.m_LogoPictureBox);
			this.Controls.Add(this.m_CenterPanel);
			this.Controls.Add(this.m_HeaderBar);
			this.Controls.Add(this.m_MainToolbar);
			this.Controls.Add(this.m_StatusBar);
			this.Font = new System.Drawing.Font("Tahoma", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.KeyPreview = true;
			this.Menu = this.m_MainMenu;
			this.Name = "MainForm";
			this.Padding = new System.Windows.Forms.Padding(2);
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "BUZM";
			this.Activated += new System.EventHandler(this.MainForm_Activated);
			this.Closing += new System.ComponentModel.CancelEventHandler(this.MainForm_Closing);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MainForm_KeyDown);
			this.Load += new System.EventHandler(this.MainForm_Load);
			this.m_CenterPanel.ResumeLayout(false);
			this.m_HeaderBar.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.m_AddressStatusPanel)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_GripStatusPanel)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_LogoPictureBox)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		/// <summary>This method used to initialize objects
		/// that the IDE Designer doesn't support </summary>
		private void InitializeManualComponents( )
		{
			// Create management and UI objs
			m_HiveView		 = new HiveView();			
			m_NetStatusPanel = new NetStatusPanel();
			m_HiveController = new HiveController( m_HiveManager );

			// Stop rendering controls
			m_StatusBar.SuspendLayout();
			m_ContentPanel.SuspendLayout();
			m_ControllerPanel.SuspendLayout();
			
			// Set all dock styles to fill
			m_HiveView.Dock			= DockStyle.Fill;
			m_HiveController.Dock	= DockStyle.Fill;
			
			// Add components to their respective containers
			m_ControllerPanel.Controls.AddRange( new System.Windows.Forms.Control[] { m_HiveController } );
			m_ContentPanel.Controls.AddRange( new System.Windows.Forms.Control[] { m_HiveView } );			
			((System.ComponentModel.ISupportInitialize)(m_NetStatusPanel)).BeginInit();
			m_StatusBar.Panels.Insert( 1, m_NetStatusPanel ); // add panel after init
			((System.ComponentModel.ISupportInitialize)(m_NetStatusPanel)).EndInit();

			// Restart control rendering
			m_ContentPanel.ResumeLayout();
			m_ControllerPanel.ResumeLayout();
			m_StatusBar.ResumeLayout();

			// Configure components
			m_VerticalSplitter.SplitPosition = m_HiveController.SnapToWidth( m_VerticalSplitter.SplitPosition );
			
			// Subscribe to necessary hive manager events and vice versa			
			m_HiveManager.HiveUpdated += new ModelEventHandler( m_HiveManager_HiveUpdated );
			m_HiveManager.PostRemoved += new ModelEventHandler( m_HiveManager_PostRemoved );
			m_HiveManager.HiveRemoved += new ModelEventHandler(  m_HiveManager_HiveRemoved );
			m_HiveManager.HiveSelected += new ModelEventHandler( m_HiveManager_HiveSelected );
			m_HiveManager.HiveUpdated += new ModelEventHandler( m_HiveView.HiveManager_HiveUpdated );
			m_HiveManager.HiveSelected += new ModelEventHandler( m_HiveView.HiveManager_HiveSelected );
			
			// bind menu items for feeds and hives directly to the hive manager and controller
			m_NewFeedMainMenuItem.Click += new System.EventHandler( m_HiveManager.NewFeed_Click );
			m_NewHiveMainMenuItem.Click += new System.EventHandler( m_HiveManager.NewHive_Click );
			m_NewMemberMainMenuItem.Click += new System.EventHandler( m_HiveManager.NewMember_Click );

			m_NewFeedTrayMenuItem.Click += new System.EventHandler( m_HiveManager.NewFeed_Click );
			m_NewHiveTrayMenuItem.Click += new System.EventHandler( m_HiveManager.NewHive_Click );
			m_NewMemberTrayMenuItem.Click += new System.EventHandler( m_HiveManager.NewMember_Click );
			m_DeleteMainMenuItem.Click += new System.EventHandler( m_HiveController.DeleteMenuItem_Click );

			// TODO: refactor to route most of the above UI events through the desktop controller
			m_HiveView.BrowserClick += new RestEventHandler( m_DeskController.HiveView_BrowserClick );
			m_DeskController.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );
			m_DeskController.PostPublished += new System.EventHandler( PostEditor_Published );

			// configure header logo and transparency
			Bitmap headerImage = new Bitmap( "Data/Resources/Header.bmp" );
			Color transColor = Color.FromArgb( 0, 255, 0 );
			headerImage.MakeTransparent( transColor );
			m_LogoPictureBox.Image = headerImage;
		
			// add debug info to window name and tray
			// if the application is running in debug mode.
			if( Debugger.IsAttached )
			{
				string serverPort	  = Config.GetIntValue( "network/defaultPort" ).ToString();
				string debugMessage	  = " - Debugging on port " + serverPort;
				m_TrayNotifyIcon.Text = m_TrayNotifyIcon.Text + debugMessage;
				this.Text = this.Text + debugMessage;

				// set debug tray icon to disinguish from other buzm instances
				m_DefaultTrayIcon = new Icon( "Data/Resources/Tray_Debug.ico" );	
			}
			else m_DefaultTrayIcon = new Icon( "Data/Resources/Tray_Default.ico" );	
			m_AlertTrayIcon = new Icon( "Data/Resources/Tray_New_Post.ico" );
			this.m_TrayNotifyIcon.Icon = m_DefaultTrayIcon;

			// restore saved window state from config file
			FormWindowState? windowState = Config.Settings.WindowState;

			if( windowState != null ) this.WindowState = (FormWindowState)windowState;
			m_LastWindowState = this.WindowState; // saved or default
		}

		#endregion

		/// <summary>Entry point for Buzm</summary>		
		[STAThread] static void Main( string[] args ) 
		{
			try // starting Buzm if another instance is not running
			{
				AppDomain.CurrentDomain.UnhandledException // bind to clr errors
				+= new UnhandledExceptionEventHandler( AppDomain_UnhandledException );

				Application.ThreadException // bind to form message pump errors
				+= new ThreadExceptionEventHandler( Application_ThreadException );

				// ensure that current directory is the install directory
				Environment.CurrentDirectory = Config.GetExecutableFolder();
				
				SingleInstance instance = new SingleInstance(); // set mutex
				if( instance.IsSingleInstance ) // if only instance running
				{
					Application.EnableVisualStyles(); // set look and feel
					Application.Run( new MainForm( args ) ); // start Buzm
				}
				else if( !instance.ActivatePriorInstance( args ) )
				{
					// Disabled multiple instance message since some users
					// might habitually double-click on the quick launch bar
					// MessageBox.Show( "Buzm is already running. If you just "
					// + "quit Buzm please wait a few seconds and try again." );
				}
				
				instance.Dispose(); // release single instance mutex
				GC.KeepAlive( instance ); // ensure mutex lifetime
			}
			catch( Exception e )
			{
				ManageUnhandledException( "Buzm needs to close due to an unexpected error. "
				+ "Please try to start Buzm again.", false, "Application Exception", e );
			}
		}

		public static void Application_ThreadException( object sender, ThreadExceptionEventArgs e )
		{
			ManageUnhandledException( "Buzm has found an unexpected error. Please try "
			+ "to exit and start Buzm again.", false, "Thread Exception", e.Exception );
		}

		public static void AppDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e )
		{
			Exception exception = e.ExceptionObject as Exception; // cast object to exception
			if( exception == null ) // if this is an unmanaged exception create a managed wrapper
			{				
				Exception inner = new Exception( e.ExceptionObject.ToString() ); // wrap long string
				exception = new Exception( "Interop exception in " + e.ExceptionObject.GetType(), inner );
			}

			ManageUnhandledException( "Buzm needs to close due to an unexpected error. "
			+ "Please try to start Buzm again.", true, "Domain Exception", exception );			
		}

		private static void ManageUnhandledException( string alert, bool exit, string source, Exception e )
		{
			string newLine = Environment.NewLine; // use the environment specific new line character/s
			
			MessageBox.Show( alert + newLine + "If this problem continues please email the following "
			+ "message to help@buzm.com:" + newLine + newLine + source + ": " + e.Message + newLine
			+ newLine + "Press Ctrl+C to copy this text and then Ctrl+V to paste it into your "
			+ "email message.  ", "Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Error );
			
			Log.Write( "Unhandled runtime exception", TraceLevel.Error, source, e );
			if( exit ) Environment.Exit( -1 ); // exit process with error code
		}
	}
}
