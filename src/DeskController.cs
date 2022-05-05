using System;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.Collections;
using Buzm.Network.Feeds;
using Buzm.Network.Web;
using Buzm.Register;
using Buzm.Schemas;
using Buzm.Hives;

namespace Buzm
{
	/// <summary>Hive controller for the desktop user interface. 
	/// Most UI control events should proxy through here.</summary>
	public class DeskController : IHiveController
	{
		private HiveManager m_HiveManager;
		public event EventHandler PostPublished;
		public event RegistryEventHandler RegistryRequest;

		public DeskController( HiveManager manager )
		{
			m_HiveManager = manager;
		}

		#region Desktop Event Handlers

		private void NewPost_Click( HiveModel hive, NameValueCollection info )
		{
			User hiveUser = null; 
			if( LoadUser( ref hiveUser ) )
			{
				Hashtable hives = m_HiveManager.HiveModels;
				if( hives.Count > 0 ) // user has at least one hive
				{
					PostEditor postEditor = new PostEditor( hiveUser, hives );
					postEditor.Published += new EventHandler( PostEditor_Published );

					if( info != null ) // if any fields are preset
					{
						postEditor.Title = info["title"];
						postEditor.Link = info["link"];	
					}
					if( hive != null ) postEditor.SelectedHive = hive;
					postEditor.Show(); // display editor to user
				}
				else
				{
					MessageBox.Show( "You must have at least one Hive to create a post.",
					"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
				}
			}
		}

		public void EditFeed_Click( HiveModel hive, FeedModel feed )
		{
			if( ( hive != null ) && ( feed != null ) ) // check params
			{
				User hiveUser = null; // check if user logged in and owns hive
				if( LoadUser( ref hiveUser ) && IsUserOwned( hive, "edits feeds" ) )
				{
					HiveModel[] ownedHives = m_HiveManager.GetUserOwnedHives();
					FeedEditor feedEditor = new FeedEditor( feed, hive, hiveUser, m_HiveManager, ownedHives );
					
					feedEditor.RegistryRequest += new RegistryEventHandler( RegistryEditor_RegistryRequest );										
					feedEditor.Show(); // display feed editor to user
				}
			}
		}

		public void HiveView_BrowserClick( object sender, RestEventArgs e )
		{
			if( ( e != null ) && e.IsLocal ) // local desktop event
			{
				RestEventRouter.ProcessRestEvent( this, e );
			}
		}

		# endregion

		#region IHiveController Methods

		public void NewPost( string hiveGuid, NameValueCollection info )
		{
			HiveModel hive = null; // map hive guid to HiveModel
			if( LoadHive( hiveGuid, ref hive ) ) NewPost_Click( hive, info );
		}

		public void EditPost( string hiveGuid, string postGuid, NameValueCollection info )
		{
			User hiveUser = null; // the user currently logged in
			HiveModel hive = null; // the hive currently being edited

			if( LoadUser( ref hiveUser ) && LoadHive( hiveGuid, ref hive ) )
			{
				ItemType post = hive.GetItemType( postGuid );
				if( post != null ) // if post deserialized
				{
					Hashtable hives = m_HiveManager.HiveModels;

					PostEditor postEditor = new PostEditor( post, hiveUser, hives );
					postEditor.Published += new EventHandler( PostEditor_Published );

					postEditor.SelectedHive = hive; // post hive
					postEditor.Show(); // display editor to user
				}
				else
				{
					MessageBox.Show( "This post no longer exists in Hive '" + hive.Name + "'.",
					"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
				}												
			}
		}

		public void RemovePost( string hiveGuid, string postGuid ) 
		{
			User hiveUser = null; // the user currently logged in
			HiveModel hive = null; // the hive currently being edited

			if( LoadUser( ref hiveUser ) && LoadHive( hiveGuid, ref hive ) )
			{
				ItemType post = hive.GetItemType( postGuid );
				if( post != null ) // if post deserialized
				{
					if( MessageBox.Show( "Are you sure you want to delete '" + post.Title
					+ "'?", "Confirm Post Delete", MessageBoxButtons.YesNo ) == DialogResult.Yes )
					{
						string login = hiveUser.Login;
						hive.RemovePost( post, login );
					}
				}
			}		
		}

		public void NewFeed( string hiveGuid, NameValueCollection info ) { }

		public void EditFeed( string hiveGuid, string feedGuid, NameValueCollection info )
		{
			HiveModel hive = null; // map hive guid to model
			if( LoadHive( hiveGuid, ref hive ) )
			{
				FeedModel feed = null; // map feed guid to model
				if( LoadFeed( hive, feedGuid, ref feed ) )
				{
					EditFeed_Click( hive, feed ); // simulate event
				}
			}
		}
		
		public void RemoveFeed( string hiveGuid, string feedGuid )
		{
			HiveModel hive = null; // hive being edited
			FeedModel feed = null; // feed to remove

			if( LoadHive( hiveGuid, ref hive ) && 
				LoadFeed( hive, feedGuid, ref feed ) )
				m_HiveManager.RemoveFeed_Click( hive, feed );
		}

		# endregion

		#region Load Validation Methods

		protected bool LoadUser( ref User user )
		{
			User hiveUser = m_HiveManager.HiveUser;
			if( hiveUser != null ) // user exists
			{
				user = hiveUser;
				return true; 
			}
			else
			{
				MessageBox.Show( "Please login or register before proceeding.",
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );				
				return false;
			}			
		}

		protected bool LoadHive( string hiveGuid, ref HiveModel hive )
		{
			Hashtable hives = m_HiveManager.HiveModels;
			if( !String.IsNullOrEmpty( hiveGuid ) && hives.Contains( hiveGuid ) )
			{
				hive = hives[hiveGuid] as HiveModel;
				if( hive != null ) return true;
			}
			else
			{
				MessageBox.Show( "The Hive being updated does not exist in your profile.",
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );								
			}
			return false; // load failed if code reached here
		}

		protected bool LoadFeed( HiveModel hive, string feedGuid, ref FeedModel feed )
		{
			Hashtable feeds = hive.Feeds; // get feeds hashtable from hive
			if( !String.IsNullOrEmpty( feedGuid ) && feeds.Contains( feedGuid ) )
			{
				feed = feeds[feedGuid] as FeedModel;
				if( feed != null ) return true;
			}
			else
			{
				MessageBox.Show( "The feed being updated does not exist in your profile.",
				"Buzm Alert", MessageBoxButtons.OK, MessageBoxIcon.Information );
			}
			return false; // load failed if code reached here
		}

		protected bool IsUserOwned( HiveModel hive, string message )
		{
			if( !hive.UserOwned ) // check current user permissions
			{
				MessageBox.Show( "You can only " +  message + " from your own Hive. '"
				+ hive.Name + "' is hosted by " + hive.Host + ".", "Buzm Alert",
				MessageBoxButtons.OK, MessageBoxIcon.Information );
				return false; // hive is not owned by user
			}
			return true; // hive is owned by user
		}

		# endregion

		#region Proxy Event Redirectors

		// proxy events in case a remote consumer cannot directly subscribe to particular handlers
		public void PostEditor_Published( object sender, EventArgs e ) { if( PostPublished != null ) PostPublished( sender, e ); }
		public void RegistryEditor_RegistryRequest( object sender, RegistryEventArgs e ) { if( RegistryRequest != null ) RegistryRequest( sender, e ); }

		# endregion
	}
}
