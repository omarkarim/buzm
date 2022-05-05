using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using Buzm.Network.Feeds;
using Buzm.Register;
using Buzm.Utility;
using Buzm.Schemas;
using NUnit.Framework;

namespace Buzm.Hives
{
	/// <summary>Provides central proxy for a set 
	/// of HiveModel functions and events</summary>
	public class HiveManager
	{
		private User m_HiveUser;
		private string m_UserFolder;
		private string m_HivesFolder;
		private string m_SkinsFolder;

		private Hashtable m_HiveModels;
		private AppVersion m_AppVersion;
		private HiveModel m_SelectedHive;
		
		public event ModelEventHandler FeedAdded;
		public event ModelEventHandler HiveAdded;
		public event ModelEventHandler FeedRemoved;
		public event ModelEventHandler HiveRemoved;
		public event ModelEventHandler PostRemoved;
		public event ModelEventHandler HiveUpdated;
		public event ModelEventHandler HiveSelected;		
		public event RegistryEventHandler RegistryRequest;

		public HiveManager( AppVersion version )
		{
			m_AppVersion = version; // save version info for hive upgrades
			m_HiveModels = new Hashtable(); // hashtable to store user hives
			m_SkinsFolder = Config.GetFolderValue( "preferences/skins/folder" );
		}

		public User HiveUser 
		{ 
			get { return m_HiveUser; }
			set { InitOrSyncUser( value ); } 
		}

		private void InitOrSyncUser( User user )
		{
			if( User.IsAlive( user ) )
			{
				if( User.IsAlive( m_HiveUser )
				&& ( m_HiveUser.Login == user.Login ) )
				{
					SynchronizeHives( user, true );
				}
				else InitializeUser( user );
			}
			else RemoveAllHives();
			m_HiveUser = user;
		}

		private void InitializeUser( User user )
		{
			RemoveAllHives(); // cleanup existing hives
			m_HiveUser = user; // update global user reference			
			
			m_UserFolder = user.DataFolder; // set user data directory
			m_HivesFolder = FileUtils.AppendSeparator( m_UserFolder ) + @"Hives/";

			// check if the user has logged in with this version before
			bool newAppVersion = !m_AppVersion.MarkerExists( m_UserFolder );
			
			InitializeHives( user, newAppVersion ); // load all user hives
			if( newAppVersion ) m_AppVersion.WriteMarker( m_UserFolder );
		}

		private void InitializeHives( User user, bool newAppVersion )
		{
			// get configured hives for user
			XmlNodeList hiveNodes = user.Hives;
			if( hiveNodes != null )
			{
				// iterate and create configured hives
				foreach( XmlNode hiveNode in hiveNodes )
				{	
					// setup hive and upgrade if needed
					AddHive( hiveNode, newAppVersion );
				}
			}
		}

		public void SynchronizeHives( User user, bool userWins )
		{
			XmlNodeList userHiveNodes = user.Hives;
			if( userHiveNodes == null ) return; // abort

			string[] mngrHiveGuids = new string[m_HiveModels.Count];
			m_HiveModels.Keys.CopyTo( mngrHiveGuids, 0 );

			List<string> loseHiveList = new List<string>();
			List<string> syncHiveList = new List<string>();

			// iterate and synchronize with manager hives
			foreach( XmlNode userHiveNode in userHiveNodes )
			{
				string hiveGuid = SafeXmlDoc.GetText( userHiveNode, "guid", "SyncHives" );
				if( !String.IsNullOrEmpty( hiveGuid ) ) // need guid to proceed
				{
					if( m_HiveModels.Contains( hiveGuid ) ) // match found
					{
						SynchronizeHive( userHiveNode, user, userWins );
						syncHiveList.Add( hiveGuid ); // for diff
					}
					else if( userWins ) AddHive( userHiveNode );
					else loseHiveList.Add( hiveGuid );
				}
			}
			foreach( string loseHiveGuid in loseHiveList )
			{
				// remove extra hives outside nodes loop
				user.RemoveHive( loseHiveGuid );
			}

			string[] syncHiveGuids = syncHiveList.ToArray(); // calc diffs
			ArrayHelper.RemoveDuplicates( ref syncHiveGuids, ref mngrHiveGuids );

			// iterate to apply differences with manager hives
			foreach( string mngrHiveGuid in mngrHiveGuids )
			{
				HiveModel hive = m_HiveModels[mngrHiveGuid] as HiveModel;
				if( hive != null ) // should always exist
				{
					if( userWins ) RemoveHive( hive );
					else user.SetHive( hive.ConfigTreeToXml() );
				}
			}
		}

		public HiveModel AddHive( XmlNode hiveNode ){ return AddHive( hiveNode, false ); }
		public HiveModel AddHive( XmlNode hiveNode, bool newAppVersion )
		{
			HiveModel hiveModel = null; // null if creation fails
			string hiveName  = Config.GetValue( hiveNode, "name" );
			string hiveGuid  = Config.GetValue( hiveNode, "guid" );
			string hiveHost  = Config.GetValue( hiveNode, "host" );
			string skinGuid  = Config.GetValue( hiveNode, "skin/guid" );
			string createDate  = Config.GetValue( hiveNode, "createDate" );

			try // setting up the hive now that we have the basic configuration
			{   // hive may not be created if an exception is thrown during setup
						
				// Get hive folder based on current user profile and the hive guid						
				string hiveFolder = m_HivesFolder + FileUtils.GuidToFileName( hiveGuid );
				if( newAppVersion || !Directory.Exists( hiveFolder ) ) // not installed
				{
					// try to install hive by copying skin from the installed folder
					string skinFolder = m_SkinsFolder + FileUtils.GuidToFileName( skinGuid );
					if( Directory.Exists( skinFolder ) ) FileUtils.CopyDirectory( skinFolder, hiveFolder );
					else throw new FileNotFoundException( "Requested Hive skin not installed", skinFolder );
				}

				// initialize the new hive and set its default properties
				hiveModel = new HiveModel( hiveName, hiveGuid, hiveFolder );
				hiveModel.SkinGuid = skinGuid; // set skin for future edits

				hiveModel.Host = hiveHost; // set host for ownership check
				hiveModel.SetOwnership( m_HiveUser ); // set owner status
				hiveModel.CreateDate = Format.StringToDate( createDate );				

				// bind hive model to proxy events so listeners can track it
				hiveModel.FeedAdded += new ModelEventHandler( HiveModel_FeedAdded );
				hiveModel.FeedRemoved += new ModelEventHandler( HiveModel_FeedRemoved );
				hiveModel.PostRemoved += new ModelEventHandler( HiveModel_PostRemoved );
				hiveModel.Updated += new ModelEventHandler(HiveModel_Updated);

				// save hive and notify any listeners
				m_HiveModels.Add( hiveGuid, hiveModel );
				OnHiveAdded( new ModelEventArgs( hiveGuid, hiveModel ) );

				// initialize registered hive feeds and members
				hiveModel.InitializeFeeds( hiveNode, m_UserFolder, this );
				hiveModel.InitializeMembers( hiveNode, m_HiveUser, this );

				// if this is the first hive select it for viewing
				if( m_HiveModels.Count == 1 ){ SelectHive( hiveModel, this ); }

				// log successful creation of the hive 
				Log.Write( "Hive added: " + hiveName, TraceLevel.Verbose, 
				"HiveManager.InitializeHives" );

				// sample code to write out html navigation
				//Trace.WriteLine( "<li><a href=\"#\" onclick=\"return loadHive('" + hiveGuid + "')\">" + hiveName + "</a></li>" );

			}
			catch( Exception e )
			{
				// log hive creation failure and the exception that caused it
				Log.Write( "Hive setup failed: " + hiveName, TraceLevel.Warning, 
				"HiveManager.InitializeHives", e );	
				
				// display error message to user - should be customized by exception type
				MessageBox.Show( "Could not load the Hive \"" + hiveName + "\" - Please make sure "
				+ "the Hive template is installed and Buzm has permission to create files.", 
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}

			return hiveModel; // return null if hive creation failed
		}

		public void SynchronizeHive( XmlNode hiveNode, User user, bool userWins )
		{
			bool hiveEdited = false; // monitor changes for refresh
			string hiveGuid = SafeXmlDoc.GetText( hiveNode, "guid", "SyncHive" );
			
			HiveModel hive = m_HiveModels[hiveGuid] as HiveModel;
			if( hive == null ) return; // no hive to synchronize

			string hiveName = SafeXmlDoc.GetText( hiveNode, "name", "SyncHive" );
			if( hiveName != hive.Name ) // sync hive name
			{
				// TODO: add name upgrade logic
				if( userWins ) { hive.Name = hiveName; hiveEdited = true; }
				else SafeXmlDoc.SetText( hiveNode, "name", hive.Name, "SyncHive" );
			}

			string skinGuid = SafeXmlDoc.GetText( hiveNode, "skin/guid", "SyncHive" );
			if( skinGuid != hive.SkinGuid ) // sync hive skin
			{
				// TODO: add skin upgrade logic
				if( userWins ) { hive.SkinGuid = skinGuid; hiveEdited = true; }
				else SafeXmlDoc.SetText( hiveNode, "skin/guid", hive.SkinGuid, "SyncHive" );
			}			
						
			bool feedsEdited = hive.SynchronizeFeeds( user, userWins, this );
			hive.SynchronizeMembers( user, userWins, this );

			// check if hive needs to be rendered again
			if( userWins && ( hiveEdited || feedsEdited ) )
			{
				hive.Rendered = false; // set update needed flag
				if( hive == m_SelectedHive ) hive.UpdateViews();
			}
		}

		public void RemoveHive( HiveModel hive ){ RemoveHive( hive, false ); }
		private void RemoveHive( HiveModel hive, bool disposing )
		{
			if( m_HiveModels.Contains( hive.Guid ) )
			{
				if( !disposing ) m_HiveModels.Remove( hive.Guid );
				hive.RemoveAllFeeds( this ); // remove registered feeds

				hive.FeedAdded -= new ModelEventHandler( HiveModel_FeedAdded );
				hive.FeedRemoved -= new ModelEventHandler( HiveModel_FeedRemoved );
				hive.PostRemoved -= new ModelEventHandler( HiveModel_PostRemoved );
				hive.Updated -= new ModelEventHandler(HiveModel_Updated);
	
				// fire an event notifying listeners of hive removal
				OnHiveRemoved( new ModelEventArgs( hive.Guid, hive ) );					
			
				// if hive is selected and session is not ending
				if( !disposing && ( hive == m_SelectedHive ) ) 
				{
					m_SelectedHive = null; // clear current selection
					foreach( HiveModel anyHive in m_HiveModels.Values )
					{
						// select the first hive in table
						SelectHive( anyHive, this ); 
						break; // exit for loop
					}
				}
			}			
		}

		private void RemoveAllHives( )
		{
			// iterate through all the active hive models
			foreach( HiveModel hive in m_HiveModels.Values )
			{
				RemoveHive( hive, true ); // unregister each hive
			}
			m_HiveModels.Clear(); // remove all hive models from table
			m_SelectedHive = null; // delete reference to current hive
		}

		public void ResetAllHiveDates( )
		{
			// iterate through all the active hive models
			foreach( HiveModel model in m_HiveModels.Values )
			{
				model.SetDefaultDateRange(); // set hive dates
				model.Rendered = false; // set update needed flag				
			}
			// render the currently selected hive for the new dates
			if( m_SelectedHive != null ) m_SelectedHive.UpdateViews();
		}

		public HiveModel SelectedHive { get { return m_SelectedHive; } }
		public void SelectHive( HiveModel hive, object controller )
		{
			if( hive != m_SelectedHive ) // if hive not selected already
			{
				if( !hive.Rendered ) hive.RenderToHtml(); // ensure html exists
				m_SelectedHive = hive; // select hive and notify listeners of event
				OnHiveSelected( new ModelEventArgs( hive.Guid, hive, controller ) );
			}
		}

		/// <summary>Gets subset of hives owned by active user</summary>
		public HiveModel[] GetUserOwnedHives( )
		{
			ArrayList ownedHives = new ArrayList( m_HiveModels.Count );
			foreach( HiveModel hive in m_HiveModels.Values )
			{
				if( hive.UserOwned ) ownedHives.Add( hive );
			}
			return (HiveModel[])ownedHives.ToArray( typeof(HiveModel) );
		}

		#region Hive Management User Interface Event Handlers & Methods

		public void NewHive_Click( object sender, System.EventArgs e )
		{
			if( m_HiveUser != null ) // check for active user
			{
				HiveEditor hiveEditor = new HiveEditor( m_HiveUser, this );
				hiveEditor.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );
				hiveEditor.Show();
			}
			else
			{
				MessageBox.Show( "Please login or register before creating a Hive.", 
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}
		}

		public void RemoveHive_Click( object sender, ModelEventArgs e )
		{
			if( ( m_HiveUser != null ) && ( e.Model is HiveModel ) ) 
			{				
				HiveDeleter hiveDeleter = new HiveDeleter( m_HiveUser, (HiveModel)( e.Model ), this );
				hiveDeleter.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );
				hiveDeleter.ShowDialog(); // process hive delete and display progress to user
			}
			else
			{
				MessageBox.Show( "Please select one of the available Hives to delete.", 
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}
		}

		public void NewFeed_Click( object sender, System.EventArgs e )
		{
			if( m_HiveUser != null ) // check for active user
			{
				HiveModel[] ownedHives = GetUserOwnedHives();
				if( ownedHives.Length > 0 ) // user owns some hives
				{
					FeedEditor feedEditor = new FeedEditor( m_HiveUser, this, ownedHives );
					feedEditor.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );
					if( m_SelectedHive != null ) feedEditor.SelectHive( m_SelectedHive );
					feedEditor.Show();
				}
				else
				{
					MessageBox.Show( "Please Create a Hive of your own before adding a feed.", 
					"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
				}
			}
			else
			{
				MessageBox.Show( "Please login or register before creating a feed.", 
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}
		}

		public void RemoveFeed_Click( HiveModel hive, FeedModel feed )
		{
			if( ( m_HiveUser != null ) && ( hive != null ) )
			{			
				if( hive.UserOwned ) // check user hive permissions
				{
					FeedDeleter feedDeleter = new FeedDeleter( m_HiveUser, hive, feed );
					feedDeleter.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );
					feedDeleter.ShowDialog(); // process feed delete and display progress to user
				}
				else
				{
					MessageBox.Show( "You can only delete feeds from your own Hive. '"
					+ hive.Name + "' is hosted by " + hive.Host + ".", "Buzm Alert",
					MessageBoxButtons.OK, MessageBoxIcon.Information );
				}
			}
			else
			{
				MessageBox.Show( "Please login or register before removing a feed.", 
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}
		}

		public void NewMember_Click( object sender, System.EventArgs e )
		{
			if( m_HiveUser != null ) // check for active user
			{
				HiveModel[] ownedHives = GetUserOwnedHives();
				if( ownedHives.Length > 0 ) // user owns some hives
				{
					MemberEditor memberEditor = new MemberEditor( m_HiveUser, this, ownedHives );
					memberEditor.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );
					if( m_SelectedHive != null ) memberEditor.SelectHive( m_SelectedHive );
					memberEditor.Show(); // display form to user
				}
				else
				{
					MessageBox.Show( "Please Create a Hive of your own before sending an invite.",
					"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
				}
			}
			else
			{
				MessageBox.Show( "Please login or register before sending an invite.",
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}
		}

		public void RemoveMember_Click( HiveModel hive, UserConfigType member )
		{
			if( ( m_HiveUser != null ) && ( hive != null ) )
			{
				if( hive.UserOwned ) // check user hive permissions
				{
					MemberDeleter memberDeleter = new MemberDeleter( m_HiveUser, hive, member );
					memberDeleter.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );
					memberDeleter.ShowDialog(); // process member delete and display progress to user
				}
				else
				{
					MessageBox.Show( "You can only delete members from your own Hive. '"
					+ hive.Name + "' is hosted by " + hive.Host + ".", "Buzm Alert",
					MessageBoxButtons.OK, MessageBoxIcon.Information );
				}
			}
			else
			{
				MessageBox.Show( "Please login or register before deleting a member.",
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}
		}
		
		#endregion

		// provide direct access to the Hive model collection
		public Hashtable HiveModels { get { return m_HiveModels; } }

		// .NET event design pattern recommends that events be wired through "OnEventName" methods
		protected void OnHiveAdded( ModelEventArgs e ){ if( HiveAdded != null ) HiveAdded( this, e ); }
		protected void OnHiveRemoved( ModelEventArgs e ){ if( HiveRemoved != null ) HiveRemoved( this, e ); }
		protected void OnHiveSelected( ModelEventArgs e ){ if( HiveSelected != null ) HiveSelected( this, e ); }

		// proxies to local object events in case a remote consumer cannot directly subscribe to particular handlers
		public void HiveModel_Updated( object sender, ModelEventArgs e ) { if( HiveUpdated != null ) HiveUpdated( this, e ); }
		public void HiveModel_FeedAdded( object sender, ModelEventArgs e ){ if( FeedAdded != null ) FeedAdded( this, e ); }
		public void HiveModel_FeedRemoved( object sender, ModelEventArgs e ){ if( FeedRemoved != null ) FeedRemoved( this, e ); }		
		public void HiveModel_PostRemoved( object sender, ModelEventArgs e ) { if( PostRemoved != null ) PostRemoved( sender, e ); }
		public void RegistryEditor_RegistryRequest( object sender, RegistryEventArgs e ){ if( RegistryRequest != null ) RegistryRequest( this, e ); }		

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class HiveManagerTest
		{			
			private string m_TempFolder;
			private HiveManager m_HiveManager;

			private ConsoleListener m_Listener;
			private const string SKIN_GUID = "3a1edc35-f826-46a0-b3bb-6005ddeae775";

			[SetUp] public void SetUp()
			{
				// bind to NUnit console listener
				m_Listener = new ConsoleListener();
				Trace.Listeners.Add( m_Listener );
				Log.TraceLevel = TraceLevel.Info;

				// load local config file for the Buzm assembly
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly );

				// create temp objects for tests to use				
				m_TempFolder = FileUtils.CreateTempFolder();
				m_HiveManager = new HiveManager( new AppVersion() );
			}

			[TearDown] public void TearDown()
			{
				// delete the test folder
				Directory.Delete( m_TempFolder, true );

				// remove NUnit console listener
				Trace.Listeners.Remove( m_Listener );

				// unload configuration or other nunit tests
				Config.UnloadConfig(); // will see it as well
			}

			[Test] public void SynchronizeHivesTest()
			{
				User origUser = new User();
				origUser.Login = "BuzmUser";

				m_HiveManager.InitializeUser( origUser );
				User syncUser = origUser.CloneIdentity();

				m_HiveManager.SynchronizeHives( syncUser, true );
				VerifySyncTest( syncUser, true, 0, new int[] { 0, 0 }, new int[] { 0, 0 }, "empty sync" );			

				// setup normal sync user
				AddTestHive( syncUser, "0", 1, 2 );
				AddTestHive( syncUser, "1", 2, 0 );

				// test sync user winning over empty original user				
				m_HiveManager.SynchronizeHives( syncUser, true );
				VerifySyncTest( syncUser, true, 2, new int[] { 1, 2 }, new int[] { 2, 0 }, "larger sync user won" );
			
				// setup larger original user
				origUser = origUser.CloneIdentity();
				AddTestHive( origUser, "0", 0, 1 );
				AddTestHive( origUser, "1", 3, 1 );
				AddTestHive( origUser, "2", 0, 0 );

				// test normal sync user winning over larger original user
				m_HiveManager.InitializeUser( origUser );
				m_HiveManager.SynchronizeHives( syncUser, true );
				VerifySyncTest( syncUser, true, 2, new int[] { 1, 2 }, new int[] { 2, 0 }, "smaller sync user won" );
				
				// setup normal original user
				origUser = origUser.CloneIdentity();
				AddTestHive( origUser, "0", 1, 0 );
				AddTestHive( origUser, "1", 2, 1 );

				// test equal sync user losing against original user
				m_HiveManager.InitializeUser( origUser );
				m_HiveManager.SynchronizeHives( syncUser, false );
				VerifySyncTest( syncUser, false, 2, new int[] { 1, 2 }, new int[] { 0, 1 }, "equal sync user lost" );
				
				// setup larger sync user
				syncUser = origUser.CloneIdentity();
				AddTestHive( syncUser, "0", 2, 1 );
				AddTestHive( syncUser, "1", 0, 1 );
				AddTestHive( syncUser, "2", 0, 0 );
				AddTestHive( syncUser, "3", 1, 1 );

				// test larger sync user losing against original user
				m_HiveManager.InitializeUser( origUser );
				m_HiveManager.SynchronizeHives( syncUser, false );
				VerifySyncTest( syncUser, false, 2, new int[] { 1, 2 }, new int[] { 0, 1 }, "larger sync user lost" );
				
				// setup smaller sync user
				syncUser = origUser.CloneIdentity();
				AddTestHive( syncUser, "0", 1, 0 );
				
				// test smaller sync user losing against original user
				m_HiveManager.InitializeUser( origUser );
				m_HiveManager.SynchronizeHives( syncUser, false );
				VerifySyncTest( syncUser, false, 2, new int[] { 1, 2 }, new int[] { 0, 1 }, "smaller sync user lost" );				
			}

			private void AddTestHive( User user, string id, int feedCount, int memCount )
			{
				HiveModel hive = new HiveModel( "H"+id, "G"+id, m_TempFolder + "G"+id );
				hive.CreateDate = DateTime.Now.Date;
				hive.Host = "BFF" + id;

				hive.SkinGuid = SKIN_GUID; 				
				user.SetHive( hive.ConfigToXml() );

				string hivePrefix = hive.Guid + "-";
				for( int x = 0; x < feedCount; x++ )
				{
					string feedPrefix = hivePrefix + "F" + x + "-";
					FeedModel feed = new FeedModel( feedPrefix + "G", feedPrefix + "U", user.DataFolder );

					feed.Name = feedPrefix + "N";
					feed.Placement = feedPrefix + "P";

					user.SetFeed( hive.Guid, feed.ConfigToXml() );
				}
				for( int y = 0; y < memCount; y++ )
				{
					string memPrefix = hivePrefix + "M" + y + "-";
					UserConfigType member = new UserConfigType();

					member.Email = memPrefix + "E";
					member.Login = memPrefix + "L";					

					member.Guid = memPrefix + "G";
					user.SetMember( hive.Guid, member.ToXml() );
				}
			}

			private void VerifySyncTest( User user, bool userWins, int hiveCount, int[] feedCount, int[] memCount, string msg )
			{
				Assert.AreEqual( hiveCount, user.Hives.Count, "Incorrect user hive count after " + msg );
				Assert.AreEqual( hiveCount, m_HiveManager.HiveModels.Count, "Incorrect manager hive count after " + msg );

				// validate specified number of test hives
				for( int x = 0; x < hiveCount; x++ )
				{
					VerifyTestHive( user, "G" + x, feedCount[x], memCount[x], msg );
				}
				
				// user xml would have matched but Hashtable.Keys.CopyTo returns a random order
				// if( !userWins ) Assert.AreEqual( m_HiveManager.HiveUser.ToXmlString(), user.ToXmlString(), "Expected users to match after " + msg );
			}

			private void VerifyTestHive( User user, string hiveGuid, int feedCount, int memCount, string msg )
			{
				XmlNode userHiveNode = user.GetHive( hiveGuid );
				Assert.IsNotNull( userHiveNode, hiveGuid + " user hive missing after " + msg );

				HiveModel hive = (HiveModel)m_HiveManager.HiveModels[hiveGuid];
				Assert.IsNotNull( hive, hiveGuid + " manager hive missing after " + msg );

				Assert.AreEqual( SafeXmlDoc.GetText( userHiveNode, "name", "" ), hive.Name, hiveGuid + " hive name mismatch after " );
				Assert.AreEqual( SafeXmlDoc.GetText( userHiveNode, "guid", "" ), hive.Guid, hiveGuid + " hive guid mismatch after " );
				Assert.AreEqual( SafeXmlDoc.GetText( userHiveNode, "host", "" ), hive.Host, hiveGuid + " hive host mismatch after " );
				Assert.AreEqual( SafeXmlDoc.GetText( userHiveNode, "skin/guid", "" ), hive.SkinGuid, hiveGuid + " hive skin mismatch after " );
				Assert.AreEqual( SafeXmlDoc.GetText( userHiveNode, "createDate", "" ), Format.DateToString( hive.CreateDate ), hiveGuid + " hive create date mismatch after " );

				// verify hive feeds
				Assert.AreEqual( feedCount, user.GetFeeds( hiveGuid ).Count, hiveGuid + " has incorrect user feed count after " + msg );
				Assert.AreEqual( feedCount, hive.Feeds.Count, hiveGuid + " has incorrect manager feed count after " + msg );

				for( int x = 0; x < feedCount; x++ )
				{
					string feedGuid = hiveGuid + "-F" + x + "-G";
					
					XmlNode feedNode = user.GetFeed( hiveGuid, feedGuid );
					Assert.IsNotNull( feedNode, feedGuid + " user feed missing after " + msg );

					FeedModel feedModel = hive.Feeds[feedGuid] as FeedModel;
					Assert.IsNotNull( feedModel, feedGuid + " manager feed missing after " + msg );

					Assert.AreEqual( feedNode.OuterXml, feedModel.ConfigToXml(), feedGuid + " config mismatch after " );
				}

				// verify hive members
				Assert.AreEqual( memCount, user.GetMembers( hiveGuid ).Count, hiveGuid + " has incorrect user member count after " + msg );
				Assert.AreEqual( memCount, hive.Members.Count, hiveGuid + " has incorrect manager member count after " + msg );

				for( int x = 0; x < memCount; x++ )
				{
					string memGuid = hiveGuid + "-M" + x + "-G";

					XmlNode memNode = user.GetMember( hiveGuid, memGuid );
					Assert.IsNotNull( memNode, memGuid + " user member missing after " + msg );

					UserConfigType member = hive.Members[memGuid] as UserConfigType;
					Assert.IsNotNull( member, memGuid + " manager member missing after " + msg );

					Assert.AreEqual( memNode.OuterXml, member.ToXml(), memGuid + " config mismatch after " );
				}
			}
		}

		#endif
		#endregion
	}
}
