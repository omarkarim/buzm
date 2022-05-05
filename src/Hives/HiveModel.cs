using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Xml.Xsl;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using NUnit.Framework;

using Buzm.Schemas;
using Buzm.Utility;
using Buzm.Register;
using Buzm.Network.Feeds;
using Buzm.Utility.Algorithms;

namespace Buzm.Hives
{
	public class HiveModel
	{
		private string m_Name;
		private string m_Guid;
		private string m_Host;
		private bool m_UserOwned;

		private bool m_Rendered;
		private string m_Folder;		
		private string m_SkinGuid;		
		private string m_InviteText;
				
		private SafeXmlDoc m_XmlDoc;
		private XslTransform m_Xslt;
		private DateTime m_EndDate;
		private DateTime m_StartDate;
		private DateTime m_CreateDate;

		// notifies HiveViews of updates
		public event ModelEventHandler Updated;
		public event ModelEventHandler PostRemoved;
		public event ModelEventHandler FeedAdded;
		public event ModelEventHandler FeedRemoved;
		public event ModelEventHandler MemberAdded;
		public event ModelEventHandler MemberRemoved;

		private const string GUID_SUFFIX = "||";
		private const string HOST_SUFFIX = " ({0})";
		private const string XML_FILE  = "hive.xml";
		private const string XSLT_FILE = "skin.xslt";
		private const string HTML_FILE = "default.htm";		
		private const int DEFAULT_DATERANGE_MINS = 8640;

		private Hashtable m_Feeds; // FeedModels for Hive
		private Hashtable m_Members; // Hive member Users
		
		// barebones constructor used for cloning
		public HiveModel( string name, string guid )
		{
			m_Name = name;
			m_Guid = guid;
			m_Rendered = false;
			m_Host = String.Empty;
			m_UserOwned = true;
		}

		public HiveModel( string name, string guid, string folder ) : this( name, guid )
		{			
			m_Feeds = new Hashtable();
			m_Members = new Hashtable();
			
			SetDefaultDateRange(); // set hive dates
			m_Folder = FileUtils.AppendSeparator( folder );

			string hiveXmlFile = m_Folder + XML_FILE;
			if( File.Exists( hiveXmlFile ) ) 
			{
				// load existing hive xml
				m_XmlDoc = new SafeXmlDoc();
				m_XmlDoc.Load( hiveXmlFile ); 
			}
			else // setup root channel for hive
			{
				ChannelType channel = new ChannelType();
				channel.Title = name; channel.Guid = guid; 
				m_XmlDoc = new SafeXmlDoc( channel.ToXml() );
				m_XmlDoc.SaveToFile( hiveXmlFile, Encoding.UTF8, "HiveModel" );
			}
		}

		#region Content Management Methods
		
		/// <summary>Adds or updates content in the hive based on its guid element</summary>
		/// <param name="contentXml">Xml fragment containing a guid child element</param>
		/// <param name="updateViews">If true, renders html and notifies views</param>
		/// <param name="notifyUser">Instructs views to notify user of update</param>
		/// <param name="save">If true, saves updated hive xml to file on disk</param>
		public bool AddContent( string contentXml ){ return AddContent( contentXml, true, false, true ); }
		public bool AddContent( string contentXml, bool updateViews, bool save ){ return AddContent( contentXml, updateViews, false, save ); }
		public bool AddContent( string contentXml, bool updateViews, bool notifyUser, bool save )
		{
			Log.Write( TraceLevel.Verbose, contentXml, "HiveModel.AddContent: " + m_Name );
			XmlNode child =  m_XmlDoc.SetUniqueChild( contentXml, "HiveModel.AddContent" );
			if( child != null ) // if the requested child was added or updated succesfully
			{
				if( updateViews ) UpdateViews( notifyUser ); // render hive to html and notify all views
				if( save ) m_XmlDoc.SaveToFile( m_Folder + XML_FILE, Encoding.UTF8, "HiveModel.AddContent" );
				if( child.ParentNode != null ) return true; // parent should only exist for new items
			}
			return false; // if set operation failed or existing item was updated
		}

		/// <summary>Removes content from the hive based on its guid child element</summary>
		/// <param name="contentGuid">Value of the guid element within content xml</param>
		/// <param name="updateViews">If true, renders html and notifies clients</param>
		/// <param name="save">If true, saves updated hive xml to file on disk</param>
		public bool RemoveContent( string contentGuid , bool updateViews, bool save )
		{
			Log.Write( TraceLevel.Verbose, contentGuid , "HiveModel.RemoveContent: " + m_Name );
			XmlNode child = m_XmlDoc.RemoveUniqueChild( contentGuid, "HiveModel.RemoveContent" );
			if( child != null ) // if the child was succesfully removed from hive xml
			{
				if( updateViews ) UpdateViews(); // render hive to html and notify all clients
				if( save ) m_XmlDoc.SaveToFile( m_Folder + XML_FILE, Encoding.UTF8, "HiveModel.RemoveContent" );
				if( child.ParentNode == null ) return true; // removed items should have no parent
			}
			return false; // if content was not found or could not be deleted
		}

		/// <summary>Adds post if newer than existing post with matching guid</summary>
		/// <param name="post">ItemType object containing the new post to merge</param>
		/// <param name="postXml">Xml representation of the new post to merge</param>
		/// <param name="notifyUser">Instructs views to notify user of update</param>
		public bool MergePost( ItemType post, string postXml, bool notifyUser )
		{
			if( ( post != null ) && !String.IsNullOrEmpty( post.Guid ) )
			{
				// try to get old post with matching guid
				ItemType oldPost = GetItemType( post.Guid );
				
				if( ( ( oldPost == null ) // post is new
				 || post.IsNewer( oldPost ) ) // update
				 && !String.IsNullOrEmpty( postXml ) )
				{					
					AddContent( postXml, true, notifyUser, true );
					return true; // post added or updated
				}
			}
			return false; // post content was not added
		}

		/// <summary>Sets deleted flag in post xml</summary>
		/// <param name="post">ItemType object to flag</param>
		/// <param name="by">User that initiated delete</param>
		public void RemovePost( ItemType post, string by )
		{
			if( ( post != null ) && !String.IsNullOrEmpty( by ) )
			{				
				post.SetDeleted( by ); 
				string postXml = post.ToXml();

				if( !String.IsNullOrEmpty( postXml ) )
				{
					// merge post in case interim edits were made
					bool merged = MergePost( post, postXml, false );

					ModelEventArgs args = new ModelEventArgs( post.Guid, post );
					if( merged && (PostRemoved != null) ) PostRemoved( this, args );					
				}
			}
		}

		/// <summary>Renders hive and fires Updated event</summary>
		public void UpdateViews(){ UpdateViews( false ); }
		public void UpdateViews( bool notifyUser )
		{
			try // rendering hive view and notifying clients
			{
				RenderToHtml(); // transform xml to html document

				ModelEventArgs args = new ModelEventArgs( m_Guid, this );
				args.NotifyUser = notifyUser; // if views must notify user

				if( m_Rendered && (Updated != null) ) Updated( this, args );	
			}
			catch( Exception e )
			{ 
				Log.Write( "Could not notify all views for hive: " + m_Name,
				TraceLevel.Error, "HiveModel.UpdateViews", e );
			}			
		}

		/// <summary>Generates an html view of the 
		/// hive xml using selected xslt skin</summary>
		public void RenderToHtml()
		{
			StreamWriter writer = null; // init null for finally
			try // transforming and saving xml to output file
			{ 
				if( !m_Rendered ) // if first render attempt
				{
					m_Xslt = new XslTransform(); // set skin
					m_Xslt.Load( m_Folder + XSLT_FILE );
				}
				
				// create an argument list to pass to the xslt
				XsltArgumentList xslArgs = new XsltArgumentList();
				xslArgs.AddExtensionObject( "urn:buzm-utility-format", new Buzm.Utility.Format() );

				// pass hive date range to constrain content window
				xslArgs.AddParam( "viewStartDateUtc", "", Format.DateToString( m_StartDate ) );
				xslArgs.AddParam( "viewEndDateUtc", "", Format.DateToString( m_EndDate.AddHours(24) ) );
				
				// transform xml doc to html and write output to file
				writer = new StreamWriter( m_Folder + HTML_FILE, false );
				m_Xslt.Transform( m_XmlDoc, xslArgs, writer, null );				
				m_Rendered = true; // set render state on success
			}
			catch( Exception e )
			{ 
				Log.Write( "Could not render to file: " + ( m_Folder + HTML_FILE ),
				TraceLevel.Error, "HiveModel.RenderToHtml", e );
				m_Rendered = false; // clear render state
			}
			finally
			{
				// ensure the output file is closed regardless of transform outcome
				if( writer != null ){ try { writer.Close(); } catch { /* ignore */ } }
			}
		}

		/// <summary>Saves any changes in hive content to the 
		/// underlying storage mechanism (currently an xml file)</summary>
		public bool SaveToStore()
		{
			string hiveXmlFile = m_Folder + XML_FILE; // save utf8 xml file in hive folder
			return m_XmlDoc.SaveToFile( hiveXmlFile, Encoding.UTF8, "HiveModel.SaveToStore" );
		}

		/// <summary>Returns a string array of guids concatenated with hashes for
		/// user posts to this Hive. The root merkle tree hash can be retrieved
		/// as an out parameter. TODO: Add date limiting criteria </summary>
		public string[] GetItemGuidsWithHashes( out byte[] rootHiveHash )
		{
			try // extracting item guids and hashes
			{
				// find all items that are children of the root channel
				XmlNodeList itemNodes = m_XmlDoc.SelectNodes( "/channel/item" );
				
				int itemNodeCount = itemNodes.Count;
				if( itemNodeCount > 0 ) // have items
				{
					// init merkle tree for root hash
					HashTree hashTree = new HashTree();

					byte[] emptyHash = hashTree.GetLeafHash( new byte[0] );
					HashTreeNode emptyHashNode = new HashTreeNode( emptyHash );
					
					HashTreeNode[] hashes = new HashTreeNode[itemNodeCount];
					string[] guids = new string[itemNodeCount];
				
					for( int i=0; i < itemNodeCount; i++ )
					{
						XmlNode itemNode = itemNodes[i];
						if( itemNode != null )
						{
							XmlNode guidNode = itemNode.SelectSingleNode( "guid" );
							if( guidNode != null )
							{								
								XmlNode hashNode = itemNode.SelectSingleNode( "hash" );
								if( hashNode != null )
								{
									string hashText = hashNode.InnerText;
									guids[i] = guidNode.InnerText + GUID_SUFFIX + hashText;

									byte[] hashBytes = Format.Base64ToBytes( hashText, emptyHash );
									hashes[i] = new HashTreeNode( hashBytes );
								}
								else
								{
									guids[i] = guidNode.InnerText;
									hashes[i] = emptyHashNode;
								}
								continue; // populated
							}
						}
						hashes[i] = emptyHashNode;
						guids[i] = String.Empty;
					}					
					
					//TODO: use culture insensitive sort comparer
					Array.Sort( guids, hashes ); // normalize arrays
					
					hashTree.BuildTree( hashes ); // build root hash
					HashTreeNode rootTreeNode = hashTree.RootTreeNode;

					if( (rootTreeNode != null) && (rootTreeNode.Hash != null) ) 
						 rootHiveHash = rootTreeNode.Hash; // set hive hash
					else rootHiveHash = new byte[0]; // no hive hash
					
					return guids; // return guids with hash suffix
				}
			}
			catch( Exception e )
			{
				Log.Write( "Could not get guids with hashes for Hive: " + m_Name,
				TraceLevel.Warning, "HiveModel.GetItemGuidsWithHashes", e );
			}
			rootHiveHash = new byte[0]; // set empty root hash
			return new string[0]; // return empty guid array
		}

		/// <summary>Retrieves existing item xml from the HiveModel</summary>
		/// <returns>An XML string or null if the item wasn't found</returns>
		public string GetItemXml( string guid )
		{
			if( guid == null ) return null;
			string realGuid = guid; // assume real
			
			int split = guid.IndexOf( GUID_SUFFIX, StringComparison.Ordinal );
			if( split != -1 ) realGuid = guid.Substring( 0, split );

			string xpath = "/channel/item[guid='" + realGuid + "']";
			XmlNode item = m_XmlDoc.GetNode( xpath, "HiveModel.GetItem" );
			
			if( item != null ) return item.OuterXml;
			else return null;
		}

		/// <summary>Retrieves an existing item type from the HiveModel</summary>
		/// <returns>An ItemType object or null if the item wasn't found</returns>
		public ItemType GetItemType( string guid )
		{
			string itemXml = GetItemXml( guid );
			if( itemXml != null ) return ItemType.FromXml( itemXml );
			else return null; // item was not found
		}

		/// <summary>Retrieves non-deleted item xml based on Title. This 
		/// method is currently not smart enough to escape quotes</summary>
		/// <returns>An XML string or null if the item wasn't found</returns>
		public string GetItemXmlByTitle( string title )
		{
			if( title == null ) return null;

			string xpath = "/channel/item[( title = \"" + title + "\" ) "
			+ "and ( string(*[local-name() = 'sync']/@deleted) != 'true' )]";

			//TODO: need custom XsltContext to escape quotes & injection attack
			XmlNode item = m_XmlDoc.GetNode( xpath, "HiveModel.GetItemXmlByTitle" );

			if( item != null ) return item.OuterXml;
			else return null;
		}

		/// <summary>Retrieves a non-deleted item type based on Title</summary>
		/// <returns>An ItemType object or null if the item wasn't found</returns>
		public ItemType GetItemTypeByTitle( string title )
		{
			string itemXml = GetItemXmlByTitle( title );
			if( itemXml != null ) return ItemType.FromXml( itemXml );
			else return null; // item was not found
		}

		#endregion

		#region Configuration Methods

		public void InitializeFeeds ( XmlNode hiveNode, string userFolder, object controller )
		{			
			XmlNodeList feedNodes = Config.GetValues( hiveNode, "feeds/feed" );
			if( feedNodes != null ) // if the feeds query succeeded
			{
				foreach( XmlNode feedNode in feedNodes )
				{
					AddFeed( feedNode, userFolder, controller );
				}
				ValidateFeeds(); // remove unregistered feeds
			}
		}

		public void AddFeed( XmlNode feedNode, string userFolder, object controller )
		{
			if( feedNode == null ) return; // no feed to add

			string feedUrl = Config.GetValue( feedNode, "url" );
			string feedGuid = Config.GetValue( feedNode, "guid" );

			string feedName = Config.GetValue( feedNode, "name" );
			string feedPlace = Config.GetValue( feedNode, "placement" );
			
			FeedModel feedModel = new FeedModel( feedGuid, feedUrl, userFolder );
			if( feedName != String.Empty ) feedModel.Name = feedName;

			feedModel.Placement = feedPlace; // set layout for feed
			AddFeed( feedModel, controller );	// add feed to table
		}

		public void AddFeed( FeedModel feed, object controller )
		{
			if( !m_Feeds.Contains( feed.Guid ) )
			{
				feed.HiveGuid = m_Guid;
				m_Feeds.Add( feed.Guid, feed ); 
				ModelEventArgs e = new ModelEventArgs( feed.Guid, feed );
				e.Controller = controller; // used to prevent event loop
				if( FeedAdded != null ) FeedAdded( this, e );
			}
		}

		public void RemoveFeed( FeedModel feed, object controller ){ RemoveFeed( feed, true, controller ); }
		public void RemoveFeed( FeedModel feed, bool save, object controller )
		{
			if( m_Feeds.Contains( feed.Guid ) )
			{
				m_Feeds.Remove( feed.Guid ); // remove feed from directory
				ModelEventArgs e = new ModelEventArgs( feed.Guid, feed, controller );
				if( FeedRemoved != null ) FeedRemoved( this, e );
				RemoveContent( feed.Guid, save, save );
			}			
		}

		public void RemoveAllFeeds( object controller )
		{
			foreach( FeedModel feed in m_Feeds.Values )
			{
				ModelEventArgs e = new ModelEventArgs( feed.Guid, feed, controller );
				e.UpdateViews = false; // so the UI can ignore this batch remove
				if( FeedRemoved != null ) FeedRemoved( this, e );
			}
			m_Feeds.Clear(); // clear the feeds hashtable
		}

		public bool SynchronizeFeeds( User user, bool userWins, object controller )
		{
			XmlNodeList userFeeds = user.GetFeeds( m_Guid );
			if( userFeeds == null ) return false; // no changes

			string[] userFeedGuids = SafeXmlDoc.GetNodeGuids( userFeeds, "HiveModel" );
			string[] hiveFeedGuids = new string[m_Feeds.Count];

			m_Feeds.Keys.CopyTo( hiveFeedGuids, 0 ); // get hive feeds to compare
			ArrayHelper.RemoveDuplicates( ref userFeedGuids, ref hiveFeedGuids );

			bool lazySave = false; // loop variable
			string userFolder = user.DataFolder;

			if( userWins ) // apply user diffs to hive feeds
			{
				foreach( string userFeedGuid in userFeedGuids )
				{
					XmlNode feedNode = user.GetFeed( m_Guid, userFeedGuid );
					if( feedNode != null ) AddFeed( feedNode, userFolder, controller );
				}
				foreach( string hiveFeedGuid in hiveFeedGuids )
				{
					FeedModel feed = m_Feeds[hiveFeedGuid] as FeedModel;
					if( feed != null ) RemoveFeed( feed, false, controller );
					lazySave = true; // save required later
				}
			}
			else // hive feeds take precedence
			{
				foreach( string userFeedGuid in userFeedGuids )
				{
					user.RemoveFeed( m_Guid, userFeedGuid );
				}
				foreach( string hiveFeedGuid in hiveFeedGuids )
				{
					FeedModel feed = m_Feeds[hiveFeedGuid] as FeedModel;
					if( feed != null ) user.SetFeed( m_Guid, feed.ConfigToXml() );					
				}				
			}
			if( lazySave ) SaveToStore(); // save changes
			return lazySave; // hive content edited
		}

		/// <summary>Verifies that no unregistered feeds are loaded in 
		/// the hive. This typically happens if the user deletes a feed
		/// on one machine and then logs in on a different one</summary>
		public void ValidateFeeds()
		{
			XmlNodeList guidNodes = m_XmlDoc.GetNodes( "/channel/channel/guid", "HiveModel.ValidateFeeds" );
			if( guidNodes != null )	// if the xpath query succeeded
			{
				bool lazySave = false; // loop variable
				foreach( XmlNode guidNode in guidNodes )
				{
					if( guidNode != null ) // if node exists
					{
						string feedGuid = guidNode.InnerText;
						if( !String.IsNullOrEmpty( feedGuid ) )
						{
							// if the feed isn't registered
							if( !m_Feeds.Contains( feedGuid ) )
							{
								RemoveContent( feedGuid, false, false );
								lazySave = true; // delay save
							}
						}
					}
				}
				// save hive if edits were made
				if( lazySave ) SaveToStore();
			}
		}

		public void InitializeMembers( XmlNode hiveNode, User hiveUser, object controller )
		{			 
			XmlNodeList memberNodes = Config.GetValues( hiveNode, "members/user" );
			if( memberNodes != null ) // if the members query succeeded
			{
				string hiveUserLogin = hiveUser.Login;
				foreach( XmlNode memberNode in memberNodes )
				{
					AddMember( memberNode, hiveUserLogin, controller );
				}
			}
		}

		public void AddMember( XmlNode memberNode, string hiveUserLogin, object controller )
		{
			if( memberNode == null ) return;
			UserConfigType member = new UserConfigType();

			member.Login = Config.GetValue( memberNode, "login" );
			if( member.Login != hiveUserLogin ) // skip hive user
			{
				member.Guid = Config.GetValue( memberNode, "guid" );
				member.Email = Config.GetValue( memberNode, "email" );
				if( member.IsValid() ) AddMember( member, controller );
			}
		}

		public void AddMember( UserConfigType member, object controller )
		{
			if( !m_Members.Contains( member.Guid ) )
			{
				m_Members.Add( member.Guid, member );
				ModelEventArgs e = new ModelEventArgs( member.Guid, member );
				e.Controller = controller; // used to prevent event loop
				if( MemberAdded != null ) MemberAdded( this, e );
			}
		}

		public void RemoveMember( UserConfigType member, object controller )
		{
			if( m_Members.Contains( member.Guid ) )
			{
				m_Members.Remove( member.Guid ); // delete reference
				ModelEventArgs e = new ModelEventArgs( member.Guid, member );
				e.Controller = controller; // used to prevent event loop
				if( MemberRemoved != null ) MemberRemoved( this, e );
			}
		}

		public void SynchronizeMembers( User user, bool userWins, object controller )
		{
			XmlNodeList userMems = user.GetMembers( m_Guid );
			if( userMems == null ) return; // members query failed

			string[] userMemGuids = SafeXmlDoc.GetNodeGuids( userMems, "HiveModel" );
			string[] hiveMemGuids = new string[m_Members.Count];

			m_Members.Keys.CopyTo( hiveMemGuids, 0 ); // get hive members
			ArrayHelper.RemoveDuplicates( ref userMemGuids, ref hiveMemGuids );

			string userLogin = user.Login;
			if( userWins ) // apply diffs to hive members
			{
				foreach( string userMemGuid in userMemGuids )
				{
					XmlNode memNode = user.GetMember( m_Guid, userMemGuid );
					if( memNode != null ) AddMember( memNode, userLogin, controller );
				}
				foreach( string hiveMemGuid in hiveMemGuids )
				{
					UserConfigType member = m_Members[hiveMemGuid] as UserConfigType;
					if( member != null ) RemoveMember( member, controller );
				}
			}
			else // hive members take precedence
			{
				foreach( string userMemGuid in userMemGuids )
				{
					user.RemoveMember( m_Guid, userMemGuid );
				}
				foreach( string hiveMemGuid in hiveMemGuids )
				{
					UserConfigType member = m_Members[hiveMemGuid] as UserConfigType;
					if( member != null ) user.SetMember( m_Guid, member.ToXml() );
				}
			}
		}

		/// <summary>Sets Hive start and end dates 
		/// to default startup configuration</summary>
		public void SetDefaultDateRange( )
		{
			m_EndDate = DateTime.Today; // set date range from today to
			TimeSpan dateRange = new TimeSpan( 0, DEFAULT_DATERANGE_MINS, 0 );
			m_StartDate = m_EndDate - dateRange; // default minutes ago
		}

		/// <summary>Sets the UserOwned flag for the hive by comparing  
		/// the hive host with the login of the specified user. Defaults
		/// to true if host is empty but may need to change that</summary>
		public void SetOwnership( User user )
		{
			if( ( m_Host == String.Empty ) || ( ( user != null ) &&
				( m_Host == user.Login ) ) ) m_UserOwned = true;
			else m_UserOwned = false; // user is not the owner
		}

		#endregion

		/// <summary>Wraps hive properties in an xml 
		/// string based on the config template </summary>
		/// <returns>String containing config xml</returns>
		public string ConfigToXml( )
		{
			// load template for hive identity from config file
			string configXml = Config.GetOuterXml( "templates/config/hive" );
			SafeXmlDoc configXmlDoc = new SafeXmlDoc( configXml ); // load xml		
			
			configXmlDoc.SetInnerText( "/hive/name", m_Name, "HiveModel.ConfigToXml" );
			configXmlDoc.SetInnerText( "/hive/guid", m_Guid, "HiveModel.ConfigToXml" );
			configXmlDoc.SetInnerText( "/hive/host", m_Host, "HiveModel.ConfigToXml" );			
			configXmlDoc.SetInnerText( "/hive/skin/guid", m_SkinGuid, "HiveModel.ConfigToXml" );			
			configXmlDoc.SetInnerText( "/hive/inviteText", m_InviteText, "HiveModel.ConfigToXml" );

			if( m_CreateDate != DateTime.MinValue ) // since minvalue is default for datetime struct
			{
				string createDate = Format.DateToString( m_CreateDate ); // get date in string format
				configXmlDoc.SetInnerText( "/hive/createDate", createDate, "HiveModel.ConfigToXml" );
			} 			
			return configXmlDoc.OuterXml; // with config information populated for the hive
		}

		/// <summary>Includes xml config for hive 
		/// children such as feeds and members</summary>	
		public string ConfigTreeToXml()
		{
			string hiveXml = this.ConfigToXml();
			if( !String.IsNullOrEmpty( hiveXml ) )
			{
				User helperUser = new User();
				helperUser.SetHive( hiveXml );

				// add feeds and members to hive config
				foreach( FeedModel feed in m_Feeds.Values )
				{
					helperUser.SetFeed( m_Guid, feed.ConfigToXml() );
				}
				foreach( UserConfigType member in m_Members.Values )
				{
					helperUser.SetMember( m_Guid, member.ToXml() );
				}
				
				XmlNode hiveNode = helperUser.GetHive( m_Guid );
				if( hiveNode != null ) return hiveNode.OuterXml;
			}
			return String.Empty; // as last resort
		}

		public override string ToString()
		{
			if( m_UserOwned ) return m_Name;
			else return m_Name + String.Format( HOST_SUFFIX, m_Host );
		}	

		public string Name
		{
			get { return m_Name; }
			set { m_Name = value; }
		}

		public string Guid
		{
			get { return m_Guid; }
			set { m_Guid = value; }
		}

		public string Host
		{
			get { return m_Host; }
			set { m_Host = value; }
		}

		public bool UserOwned
		{
			get { return m_UserOwned; }
		}

		public bool Rendered
		{
			get { return m_Rendered; }
			set { m_Rendered = value; }
		}

		public string SkinGuid
		{
			get { return m_SkinGuid; }
			set { m_SkinGuid = value; }
		}

		public string InviteText
		{
			get { return m_InviteText; }
			set { m_InviteText = value; }
		}

		public string Url
		{
			get { return m_Folder + HTML_FILE; }
		}

		public DateTime CreateDate
		{
			get { return m_CreateDate; }
			set { m_CreateDate = value; }
		}

		public DateTime StartDate
		{
			get { return m_StartDate; }
			set { m_StartDate = value; }
		}

		public DateTime EndDate
		{
			get { return m_EndDate; }
			set { m_EndDate = value; }
		}

		public Hashtable Feeds
		{
			get { return m_Feeds; }
			set { m_Feeds = value; }
		}

		public Hashtable Members
		{
			get { return m_Members; }
			set { m_Members = value; }
		}

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class HiveModelTest
		{
			string m_Guid;
			HiveModel m_Hive;
			string m_TempFolder;			
			ConsoleListener m_Listener;
			private const int LARGE_HIVE_ITEMS = 1000;

			[SetUp] public void SetUp() 
			{ 			
				// bind to NUnit console listener
				m_Listener = new ConsoleListener();				
				Trace.Listeners.Add( m_Listener );
				Log.TraceLevel = TraceLevel.Info;

				// load local config file for the Buzm assembly
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly );

				// create global hive for tests to use
				m_Guid = System.Guid.NewGuid().ToString();
				m_TempFolder = FileUtils.CreateTempFolder();
				m_Hive = new HiveModel( "Test Hive", m_Guid, m_TempFolder );
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

			[Test] public void HiveCreateTest()
			{
				// check data points for test Hive created during setup
				Assert.AreEqual( m_Guid, m_Hive.m_XmlDoc.GetInnerText( "/channel/guid", "" ), "Got incorrect guid from new hive" );
				Assert.AreEqual( "Test Hive", m_Hive.m_XmlDoc.GetInnerText( "channel/title", "" ), "Got incorrect title from new hive" );
			
				// reload created hive into memory and compare known data points
				HiveModel oldHive = new HiveModel( "Test Hive", m_Guid, m_TempFolder );			
				Assert.AreEqual( m_Guid, oldHive.m_XmlDoc.GetInnerText( "/channel/guid", "" ), "Got incorrect guid from old hive" );
				Assert.AreEqual( "Test Hive", oldHive.m_XmlDoc.GetInnerText( "channel/title", "" ), "Got incorrect title from old hive" );	
			}

			[Test] public void AddContentTest()
			{
				// xpath for unique content
				string guidXPathSuffix = "']";
				string guidXPathPrefix = "/channel/*[guid = '";				

				bool result = m_Hive.AddContent( "" ); 
				Assert.IsFalse( result, "Tried adding an empty item");

				result = m_Hive.AddContent( "<badxml<>" ); 
				Assert.IsFalse( result, "Tried adding an item with invalid xml" );	
		
				// add valid item to hive xml
				ItemType newItem = new ItemType();
				newItem.Title = "Test Hive Item One";
				result = m_Hive.AddContent( newItem.ToXml(), true, false );
				SafeXmlDoc hiveXmlDoc = m_Hive.m_XmlDoc;
			
				// check if item was added to the xml document
				XmlNode itemNode = hiveXmlDoc.SelectSingleNode( guidXPathPrefix + newItem.Guid + guidXPathSuffix );			
				Assert.IsNotNull( itemNode, "New item not found in Hive" ); // new item node should exist in xml doc
				Assert.AreEqual( newItem.Title, itemNode.SelectSingleNode( "title" ).InnerText, "Incorrect title for new item." );
				Assert.AreEqual( Format.DateToString( newItem.Posted ), itemNode.SelectSingleNode( "posted" ).InnerText, "Incorrect posted date for new item." );

				// check to see if xml document was saved to disk even though save was set to false in AddContent call
				SafeXmlDoc diskXmlDoc = new SafeXmlDoc(); // create doc to load saved output xml
				diskXmlDoc.LoadFromFile( m_TempFolder + HiveModel.XML_FILE, "AddContentTest" );
				itemNode = diskXmlDoc.SelectSingleNode( guidXPathPrefix + newItem.Guid + guidXPathSuffix );			
				Assert.IsNull( itemNode, "New item was found on disk though it should not have been saved" ); 

				// change item title and add again
				newItem.Title = "Updated Item One";
				LoadStylesheet(); // set temp stylesheet for transform
				result = m_Hive.AddContent( newItem.ToXml(), false, false );

				// check if item was updated in xml document
				itemNode = hiveXmlDoc.SelectSingleNode( guidXPathPrefix + newItem.Guid + guidXPathSuffix );
				Assert.AreEqual( newItem.Title, itemNode.SelectSingleNode( "title" ).InnerText, "Incorrect title for updated item." );
				Assert.AreEqual( 1, hiveXmlDoc.SelectNodes( "/channel/item").Count, "Should only be one item in Hive" );

				// check to see if output html file was created even though render was set to false in AddContent call
				Assert.IsFalse( File.Exists( m_TempFolder + HiveModel.HTML_FILE ), "HTML file created though render was false" );

				// create new channel and add without guid
				ChannelType newChannel = new ChannelType();
				newChannel.Guid = null; // clear default guid
				result = m_Hive.AddContent( newChannel.ToXml() );
				Assert.IsFalse( result, "Tried adding a channel without guid" );
			
				// set channel guid and title and try again
				newChannel.Guid = System.Guid.NewGuid().ToString();
				newChannel.Title = "Test Hive Channel One";
				result = m_Hive.AddContent( newChannel.ToXml(), false, true );

				// check if channel was added to the xml doc
				XmlNode channelNode = hiveXmlDoc.SelectSingleNode( guidXPathPrefix + newChannel.Guid + guidXPathSuffix );
				Assert.IsNotNull( channelNode, "New channel not found in Hive" ); // new channel node should exist in xml doc
				Assert.AreEqual( newChannel.Title, channelNode.SelectSingleNode( "title" ).InnerText, "Incorrect title for new channel." );

				// check if hive xml doc was saved to disk with the correct data
				diskXmlDoc = new SafeXmlDoc(); // create doc to load saved output xml
				diskXmlDoc.LoadFromFile( m_TempFolder + HiveModel.XML_FILE, "AddContentTest" );
				channelNode = diskXmlDoc.SelectSingleNode( guidXPathPrefix + newChannel.Guid + guidXPathSuffix );
				Assert.IsNotNull( channelNode, "New channel not found on disk" ); // new channel node should exist on disk
				Assert.AreEqual( newChannel.Title, channelNode.SelectSingleNode( "title" ).InnerText, "Incorrect title for channel on disk." );

				// change channel title and add again
				newChannel.Title = "Updated Channel One";
				result = m_Hive.AddContent( newChannel.ToXml(), true, true );

				// check if existing channel was updated
				channelNode = hiveXmlDoc.SelectSingleNode( guidXPathPrefix + newChannel.Guid + guidXPathSuffix );
				Assert.AreEqual( newChannel.Title, channelNode.SelectSingleNode( "title" ).InnerText, "Incorrect title for updated channel." );
				Assert.AreEqual( 1, hiveXmlDoc.SelectNodes( "/channel/channel").Count, "Should only be one channel in Hive" );

				// check to see if output html file was created on disk since render was set to true in AddContent call
				Assert.IsTrue( File.Exists( m_TempFolder + HiveModel.HTML_FILE ), "HTML file not created though render was true" );				

				// set channel guid to equal existing item guid
				newChannel.Guid = newItem.Guid;
				result = m_Hive.AddContent( newChannel.ToXml() );
			
				// check if channel replaced the item with the same guid
				channelNode = hiveXmlDoc.SelectSingleNode( guidXPathPrefix + newChannel.Guid + guidXPathSuffix );
				Assert.AreEqual( newChannel.Title, channelNode.SelectSingleNode( "title" ).InnerText, "Incorrect title for swapped channel." );
				Assert.AreEqual( 2, hiveXmlDoc.SelectNodes( "/channel/channel").Count, "Should now be two channels in Hive" );
				Assert.AreEqual( 0, hiveXmlDoc.SelectNodes( "/channel/item").Count, "Channel should have replaced the item" );
			}

			[Test] public void RemoveContentTest()
			{
				SafeXmlDoc hiveXmlDoc = m_Hive.m_XmlDoc;
				string rootXPath = "/channel"; // hive root
				string guid = System.Guid.NewGuid().ToString();
				LoadStylesheet(); // set stylesheet for transforms

				// try to remove non-existent item from the hive
				bool result = m_Hive.RemoveContent( guid, true, true );
				Assert.IsFalse( result, "Tried to remove non-existent item from Hive" );			

				// setup valid new item for hive
				ItemType newItem = new ItemType();				
				newItem.Title = "Test Hive Item One";
				newItem.Guid = guid; // use pregen guid
				
				// try to add valid item to hive xml 
				result = m_Hive.AddContent( newItem.ToXml(), false, true );
				Assert.IsTrue( result, "Could not add test content to the Hive" );
			
				// check if item was added to the xml document
				XmlNode itemNode = hiveXmlDoc.GetUniqueChild( rootXPath, guid, "RemoveContentTest" );
				Assert.IsNotNull( itemNode, "New item not found in Hive" ); // new item node should exist in xml doc
				
				// check to see if xml document was saved to disk with new item
				SafeXmlDoc diskXmlDoc = new SafeXmlDoc(); // create doc to load saved xml
				diskXmlDoc.LoadFromFile( m_TempFolder + HiveModel.XML_FILE, "RemoveContentTest" );
				itemNode = diskXmlDoc.GetUniqueChild( rootXPath, guid, "RemoveContentTest" );
				Assert.IsNotNull( itemNode, "New item was not found in Hive document on disk " ); 				

				// try to remove new item from the hive
				result = m_Hive.RemoveContent( guid, true, true );
				Assert.IsTrue( result, "Tried to remove existing item from Hive" );

				// check if item was removed from the xml document
				itemNode = hiveXmlDoc.GetUniqueChild( rootXPath, guid, "RemoveContentTest" );
				Assert.IsNull( itemNode, "Removed item found in Hive" ); // removed item should not exist

				// check to if xml document was saved without removed item
				diskXmlDoc = new SafeXmlDoc(); // create doc to load saved xml
				diskXmlDoc.LoadFromFile( m_TempFolder + HiveModel.XML_FILE, "RemoveContentTest" );
				itemNode = diskXmlDoc.GetUniqueChild( rootXPath, guid, "RemoveContentTest" );
				Assert.IsNull( itemNode, "Removed item found in Hive document on disk " ); 				

				// check to see if output html file was created on disk since render was set to true in RemoveContent call
				Assert.IsTrue( File.Exists( m_TempFolder + HiveModel.HTML_FILE ), "HTML file not created though render was true" );	
			}

			[Test] public void RenderToHtmlTest()
			{ 				
				m_Hive.RenderToHtml(); // and try to transform without existing xslt file
				Assert.IsFalse( File.Exists( m_TempFolder + HiveModel.HTML_FILE ), "HTML file created without xslt" );
				
				m_Hive.Rendered = true; // artifically set rendered state to true								
				m_Hive.RenderToHtml(); // and try to transform without loading xslt file
				Assert.IsFalse( m_Hive.Rendered, "Render state set to true without xslt file" );

				LoadStylesheet(); // create temp stylesheet for hive
				SafeXmlDoc hiveXmlDoc = m_Hive.m_XmlDoc; // backup xml doc
				m_Hive.m_XmlDoc = null;  // temporarily clear xml doc from hive

				m_Hive.RenderToHtml(); // try to transform hive without xml document
				Assert.IsFalse( m_Hive.Rendered, "Render state set to true without xml doc" );
				
				m_Hive.m_XmlDoc = hiveXmlDoc; // reset hive xml doc
				m_Hive.StartDate = DateTime.MinValue; // this date should work fine
				m_Hive.EndDate = DateTime.MaxValue; // invalid since 24 hours will be added
				
				m_Hive.RenderToHtml(); // try to transform hive with invalid date range
				Assert.IsFalse( m_Hive.Rendered, "Render state set to true with invalid date range" );

				TimeSpan oneDay = new TimeSpan( 24, 0, 0 );
				m_Hive.EndDate = DateTime.MaxValue.Subtract( oneDay );

				m_Hive.RenderToHtml(); // try to transform hive with valid date range
				Assert.IsTrue( m_Hive.Rendered, "Render state set to false with valid date range" );

				SafeXmlDoc htmlDoc = new SafeXmlDoc(); // create doc to load rendered html
				htmlDoc.LoadFromFile( m_TempFolder + HiveModel.HTML_FILE, "RenderToHtmlTest" );

				string outputStartDate = htmlDoc.GetInnerText( "/html/body/div[1]", "RenderToHtmlTest" );
				string outputEndDate = htmlDoc.GetInnerText( "/html/body/div[2]", "RenderToHtmlTest" );

				Assert.AreEqual( Format.DateToString( m_Hive.StartDate ), outputStartDate, "Incorrect start date in output html" );
				Assert.AreEqual( Format.DateToString( m_Hive.EndDate.AddHours( 24 ) ), outputEndDate, "Incorrect end date in output html" );

				// add a test channel to the hive
				ChannelType channel = new ChannelType();						
				channel.Guid = System.Guid.NewGuid().ToString();
				channel.Title = "Test Hive Channel One";
				m_Hive.AddContent( channel.ToXml(), false, true );

				// add a test item to the hive
				ItemType itemOne = new ItemType();
				itemOne.Title = "Test Hive Item One";
				ItemType itemTwo = new ItemType();
				itemTwo.Title = "Test Hive Item Two";
				m_Hive.AddContent( itemOne.ToXml(), false, true );
				m_Hive.AddContent( itemTwo.ToXml(), false, true );

				m_Hive.RenderToHtml(); // try to transform hive with new content added
				Assert.IsTrue( m_Hive.Rendered, "Render state set to false with valid date range" );

				htmlDoc = new SafeXmlDoc(); // create doc to load rendered html
				htmlDoc.LoadFromFile( m_TempFolder + HiveModel.HTML_FILE, "RenderToHtmlTest" );

				string itemOneTitle = htmlDoc.GetInnerText( "/html/body/table[1]/tr[1]/td[1]", "RenderToHtmlTest" );
				string itemTwoTitle = htmlDoc.GetInnerText( "/html/body/table[1]/tr[3]/td[1]", "RenderToHtmlTest" );
				string channelTitle = htmlDoc.GetInnerText( "/html/body/table[2]/tr[1]/td[1]", "RenderToHtmlTest" );

				Assert.AreEqual( itemOne.Title, itemOneTitle, "Incorrect title for first item in html output" );
				Assert.AreEqual( itemTwo.Title, itemTwoTitle, "Incorrect title for second item in html output" );
				Assert.AreEqual( channel.Title, channelTitle, "Incorrect title for first channel in html output" );
			}

			[Test] public void PerformanceTest()
			{
				// time population of large hive
				DateTime startTime = DateTime.Now;
				LoadLargeHive( m_Hive ); // approx 3MB
				TimeSpan duration = DateTime.Now - startTime;
				Log.Write( TraceLevel.Info, "Large hive populated in: " + duration.ToString(), "HiveModelTest.RenderToHtmlTest" );
					
				ItemType item = new ItemType();
				item.Title = "Test Hive Item 1";
	
				// time saving of large hive
				startTime = DateTime.Now;
				m_Hive.AddContent( item.ToXml(), false, true );
				duration = DateTime.Now - startTime; // calculate duration
				Log.Write( TraceLevel.Info, "Large hive saved in: " + duration.ToString(), "HiveModelTest.RenderToHtmlTest" );					

				// time transformation of large hive
				LoadStylesheet(); // temp xslt
				startTime = DateTime.Now;
				m_Hive.RenderToHtml(); // transform
				duration = DateTime.Now - startTime;
				Log.Write( TraceLevel.Info, "Large hive transformed in: " + duration.ToString(), "HiveModelTest.RenderToHtmlTest" );
			}

			[Test] public void GetItemAndGuidsWithHashesTest()
			{
				const int ITEM_COUNT = 10;
				ItemType item; // loop var
				byte[] rootHiveHash; // merkle root

				string itemTitle = "Title><&'/>";
				ItemType[] items = new ItemType[ITEM_COUNT];

				// try to get item guids from empty hive
				string[] itemGuids = m_Hive.GetItemGuidsWithHashes( out rootHiveHash );
				Assert.AreEqual( 0, itemGuids.Length, "Got incorrect count from empty hive" );
				Assert.AreEqual( 0, rootHiveHash.Length, "Got incorrect hash from empty hive" );
				
				// try to get non-existent item from hive
				string nullItem = m_Hive.GetItemXml( "nullItemGuid" );
				ItemType nullItemType = m_Hive.GetItemType( "nullItemGuid" );

				Assert.IsNull( nullItem, "Got non-existent item from hive" );
				Assert.IsNull( nullItemType, "Got non-existent item type from hive" );

				// try to get item with null guid
				nullItem = m_Hive.GetItemXml( null );
				nullItemType = m_Hive.GetItemType( null );

				Assert.IsNull( nullItem, "Got item from null guid" );
				Assert.IsNull( nullItemType, "Got item type from null guid" );

				// add a single item without hash
				item = new ItemType();
				item.Guid = "0guid";
				item.Title = "0" + itemTitle;

				items[0] = item; // save item for test result
				m_Hive.AddContent( item.ToXml(), false, false );
		
				// validate item count and well-known root hash
				itemGuids = m_Hive.GetItemGuidsWithHashes( out rootHiveHash );
				Assert.AreEqual( 1, itemGuids.Length, "Got incorrect guid count after first item" );

				Assert.AreEqual( 20, rootHiveHash.Length, "Got incorrect Sha1 length after first item" );
				Assert.AreEqual( "W6k8nbDP+T9StSHXQg5D9u2ieE8=", Convert.ToBase64String( rootHiveHash ), "Got incorrect root hash after first item" );

				// add more items with hash nodes
				for( int i=1; i < ITEM_COUNT; i++ )
				{
					item = new ItemType( "buzmer" );
					item.Guid = i.ToString() + "guid";
					
					item.Title = i.ToString() + itemTitle;
					item.Summary = ">some invalid chars <<";
					
					m_Hive.AddContent( item.ToXml(), false, false );
					items[i] = item; // save item for test result
				}
				
				itemGuids = m_Hive.GetItemGuidsWithHashes( out rootHiveHash );
				Assert.AreEqual( ITEM_COUNT, itemGuids.Length, "Got incorrect guid count" );				
				Assert.AreEqual( 20, rootHiveHash.Length, "Got incorrect Sha1 length" );

				// get items and compare info
				for( int i=0; i < ITEM_COUNT; i++ )
				{					
					if( i == 0 ) Assert.AreEqual( items[i].Guid, itemGuids[i], "Got incorrect item guid without hash" );
					else Assert.AreEqual( items[i].Guid + HiveModel.GUID_SUFFIX + items[i].Hash, itemGuids[i], "Got incorrect item guid with hash" );
					
					Assert.AreEqual( items[i].ToXml(), m_Hive.GetItemXml( itemGuids[i] ), "Got incorrect item xml" );					
					Assert.AreEqual( items[i].Guid, m_Hive.GetItemType( itemGuids[i] ).Guid, "Got incorrect item type guid" );
					
					Assert.AreEqual( items[i].Title, m_Hive.GetItemType( itemGuids[i] ).Title, "Got incorrect item type title" );
					Assert.AreEqual( items[i].ToXml(), m_Hive.GetItemXmlByTitle( i.ToString() + itemTitle ), "Got incorrect item by title" );
				}
					
				// get guids from large hive
				LoadLargeHive( m_Hive );  // 3MB
				DateTime startTime = DateTime.Now;
				
				itemGuids = m_Hive.GetItemGuidsWithHashes( out rootHiveHash );
				Assert.AreEqual( LARGE_HIVE_ITEMS + ITEM_COUNT, itemGuids.Length, "Got incorrect guid count from large hive" );
				Assert.AreEqual( 20, rootHiveHash.Length, "Got incorrect Sha1 length for large hive" );

				TimeSpan duration = DateTime.Now - startTime; // calculate duration				
				Log.Write( TraceLevel.Info, "Got large hive guids in: " + duration.ToString(), "HiveModelTest.GetItemAndGuidsTest" );			
			}

			[Test] public void GetItemByTitleTest()
			{
				// try title with special chars
				string itemTitle = "Title><&'/>";
				ItemType itemOne = new ItemType();
				itemOne.Title = itemTitle;
				
				m_Hive.AddContent( itemOne.ToXml(), false, false );
				string itemXml = m_Hive.GetItemXmlByTitle( itemTitle );
				ItemType itemType = m_Hive.GetItemTypeByTitle( itemTitle );

				Assert.IsNotNull( itemXml, "Failed to get item by title" );
				Assert.AreEqual( itemOne.ToXml(), itemXml, "Got incorrect item by title" );

				Assert.IsNotNull( itemType, "Failed to get item type by title" );
				Assert.AreEqual( itemOne.Guid, itemType.Guid, "Got incorrect item type by title" );

				// add another item with same title
				ItemType itemTwo = new ItemType();
				itemTwo.Title = itemTitle;
				
				m_Hive.AddContent( itemTwo.ToXml(), false, false );
				string itemXmlAgain = m_Hive.GetItemXmlByTitle( itemTitle );
				ItemType itemTypeAgain = m_Hive.GetItemTypeByTitle( itemTitle );

				Assert.IsNotNull( itemXmlAgain, "Failed to get duplicate item by title" );
				Assert.AreEqual( itemOne.ToXml(), itemXmlAgain, "Got incorrect duplicate item by title" );

				Assert.IsNotNull( itemTypeAgain, "Failed to get duplicate item type by title" );
				Assert.AreEqual( itemOne.Guid, itemTypeAgain.Guid, "Got incorrect duplicate item type by title" );

				// add item with quotes in title
				ItemType itemThree = new ItemType();
				itemThree.Title = "Quoted\"Title";

				// GetItemXmlByTitle does not escape quotes yet
				m_Hive.AddContent( itemThree.ToXml(), false, false );				 
				itemXml = m_Hive.GetItemXmlByTitle( "Quoted\"Title" );
				itemType = m_Hive.GetItemTypeByTitle( "Quoted\"Title" );

				Assert.IsNull( itemXml, "Got item by title with quotes" );
				Assert.IsNull( itemType, "Got item type by title with quotes" );

				// try item with sync info				
				ItemType itemFour = new ItemType( "buzm" );
				itemFour.Title = "synctitle";

				m_Hive.AddContent( itemFour.ToXml(), false, false );
				itemXml = m_Hive.GetItemXmlByTitle( "synctitle" );
				itemType = m_Hive.GetItemTypeByTitle( "synctitle" );

				Assert.IsNotNull( itemXml, "Failed to get sync item by title" );
				Assert.AreEqual( itemFour.ToXml(), itemXml, "Got incorrect sync item by title" );

				Assert.IsNotNull( itemType, "Failed to get sync item type by title" );
				Assert.AreEqual( itemFour.Guid, itemType.Guid, "Got incorrect sync item type by title" );

				// set item sync to deleted
				itemFour.SetDeleted( "omar" );

				m_Hive.AddContent( itemFour.ToXml(), false, false );
				itemXml = m_Hive.GetItemXmlByTitle( "synctitle" );
				itemType = m_Hive.GetItemTypeByTitle( "synctitle" );

				Assert.IsNull( itemXml, "Got deleted item by title" );
				Assert.IsNull( itemType, "Got deleted item type by title" );

				// try to get non-existent item from hive
				itemXml = m_Hive.GetItemXmlByTitle( "randomtitle" );
				itemType = m_Hive.GetItemTypeByTitle( "randomtitle" );

				Assert.IsNull( itemXml, "Got non-existent item from hive" );
				Assert.IsNull( itemType, "Got non-existent item type from hive" );

				// try to get item with null title
				itemXml = m_Hive.GetItemXmlByTitle( null );
				itemType = m_Hive.GetItemTypeByTitle( null );

				Assert.IsNull( itemXml, "Got item from null title" );
				Assert.IsNull( itemType, "Got item type from null title" );
			}

			[Test] public void ConfigToXmlTest()
			{
				// load hive config into xml document
				SafeXmlDoc identityXmlDoc = new SafeXmlDoc( m_Hive.ConfigToXml() );
				string hiveName = identityXmlDoc.GetInnerText( "/hive/name", "ConfigToXmlTest" );
				string hiveGuid = identityXmlDoc.GetInnerText( "/hive/guid", "ConfigToXmlTest" );
				string skinGuid = identityXmlDoc.GetInnerText( "/hive/skin/guid", "ConfigToXmlTest" );
				string createDate = identityXmlDoc.GetInnerText( "/hive/createDate", "ConfigToXmlTest" );
				string host = identityXmlDoc.GetInnerText( "/hive/host", "ConfigToXmlTest" );

				// check output values against input values
				Assert.AreEqual( m_Guid, hiveGuid, "Got incorrect guid from hive" );
				Assert.AreEqual( "Test Hive", hiveName, "Got incorrect name from hive" );				
				Assert.AreEqual( String.Empty, skinGuid, "Got incorrect skin guid from hive" );
				Assert.AreEqual( String.Empty, createDate, "Got incorrect create date from hive" );
				Assert.AreEqual( String.Empty, host, "Got incorrect host from hive" );
			
				m_Hive.Host = "okarim"; // set dummy hive properties
				m_Hive.SkinGuid = "3633bcc2-faef-4ee0-bbee-e00f03fefeaf";
				m_Hive.InviteText = "Hey, check out the cool hive I have";
				m_Hive.CreateDate = new DateTime( 2010, 10, 10, 10, 10, 10 );

				identityXmlDoc = new SafeXmlDoc( m_Hive.ConfigToXml() );			
				skinGuid = identityXmlDoc.GetInnerText( "/hive/skin/guid", "ConfigToXmlTest" );
				createDate = identityXmlDoc.GetInnerText( "/hive/createDate", "ConfigToXmlTest" );
				string inviteText = identityXmlDoc.GetInnerText( "/hive/inviteText", "ConfigToXmlTest" );
				host = identityXmlDoc.GetInnerText( "/hive/host", "ConfigToXmlTest" );				
			
				Assert.AreEqual( "okarim", host, "Got incorrect host from hive" );				
				Assert.AreEqual( "3633bcc2-faef-4ee0-bbee-e00f03fefeaf", skinGuid, "Got incorrect skin guid from hive" );
				Assert.AreEqual( "Hey, check out the cool hive I have", inviteText, "Got incorrect invite text from hive" );
				Assert.AreEqual( Format.DateToString( m_Hive.CreateDate ), createDate, "Got incorrect create date from hive" );
			}

			private void LoadLargeHive( HiveModel hive )
			{
				ItemType item;
				ChannelType channel;

				string baseChannelTitle = "This is my wonderful channel";
				string baseItemTitle = "This is supposed to be a relatively long title for a test story";
				string baseItemSummary = "Roland Piquepaille writes Tele-immersion is a technology which allows cooperative interaction between groups of distant people working in the same virtual environment. At the Center for Information Technology Research in the Interest of Society (CITRIS) at UC Berkeley, interdisciplinary teams are deploying this technology. It involves three real-time steps: taking images of a subject with 48 cameras, transmitting the images over a network, and implanting them in a virtual world. For example, it will allow students and professors on different campuses to meet -- virtually -- and discuss -- lively -- while being in ancient sites of Greece or Italy. The technology offers more promises than academics discussions. Imagine a nurse telling a diabetic how to make an insulin injection while being far away from him. Of course, this technology is facing some hurdles, such as the cost involved to model you with so many cameras. This summary shows you some details about the image processing involved in this project.";

				// add 1000 items to the hive
				for( int i=0; i < LARGE_HIVE_ITEMS; i++ )
				{
					item = new ItemType( "buzmuser" );
					item.Position = "1,1"; // top-center
					item.Title = i.ToString() + baseItemTitle;
					item.Summary = i.ToString() + baseItemSummary;
					hive.AddContent( item.ToXml(), false, false );
				}

				// add 100 channels to the hive
				for( int i=0; i < 100; i++ )
				{
					channel = new ChannelType();
					channel.Position = "0,1"; // top-left
					channel.Guid = System.Guid.NewGuid().ToString();
					channel.Title = i.ToString() + baseChannelTitle;
					
					// add 10 items for each channel
					for( int x=0; x < 10; x++ )
					{
						item = new ItemType();
						item.Position = "1,1"; // top-center
						item.Title = i.ToString() + x.ToString() + baseItemTitle;
						item.Summary = i.ToString() + x.ToString() + baseItemSummary;
						channel.Items.Add( item ); // add item to the channel
					}
					// add channel to the hive
					hive.AddContent( channel.ToXml(), false, false );
				}
			}

			private void LoadStylesheet()
			{
				// create hardcoded temp stylesheet to test Hive html transformation
				SafeXmlDoc xsltDoc = new SafeXmlDoc(@"<?xml version='1.0' encoding='utf-8'?>
					<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0'>
						<xsl:output method='xml' version='1.0' encoding='utf-8' indent='yes'/>
						<xsl:param name='viewStartDateUtc' select='default'/>
                        <xsl:param name='viewEndDateUtc' select='default'/>
						<xsl:template match='/'>
							<html>
								<head><title><xsl:value-of select='/channel/title'/></title></head>
								<body>
									<div><xsl:value-of select='$viewStartDateUtc'/></div>
									<div><xsl:value-of select='$viewEndDateUtc'/></div>	
									<table>
										<xsl:for-each select='/channel/item'>
											<tr><td><xsl:value-of select='title' disable-output-escaping='yes'/></td></tr>
											<tr><td><xsl:value-of select='summary' disable-output-escaping='yes'/></td></tr>
										</xsl:for-each>
									</table>
									<table>
										<xsl:for-each select='/channel/channel'>
											<tr><td><xsl:value-of select='title' disable-output-escaping='yes'/></td></tr>
										</xsl:for-each>
									</table>
								</body>
							</html>
						</xsl:template>
					</xsl:stylesheet>");
	
				// save new stylesheet to temp folder so it can be used in the transform
				xsltDoc.SaveToFile( m_TempFolder + HiveModel.XSLT_FILE, Encoding.UTF8, "" );
			}

			[Test] public void SetOwnershipTest()
			{
				User user = new User();
				user.Login = "okarim";

				m_Hive.Host = null; 
				m_Hive.SetOwnership( user ); // with null host which should never be the case
				Assert.IsFalse( m_Hive.UserOwned, "Null host incorrectly returned true for user" );

				m_Hive.Host = String.Empty; 
				m_Hive.SetOwnership( user ); // with empty host which is normal for self created hives
				Assert.IsTrue( m_Hive.UserOwned, "Empty host incorrectly returned false for user" );

				m_Hive.Host = "okarim";
				m_Hive.SetOwnership( user ); // with user as host which might be the norm in the future
				Assert.IsTrue( m_Hive.UserOwned, "Self host incorrectly returned false for user" );

				m_Hive.Host = "other"; 
				m_Hive.SetOwnership( user ); // with other host which is normal for invited hives
				Assert.IsFalse( m_Hive.UserOwned, "Other host incorrectly returned true for user" );

				m_Hive.Host = "other"; 
				m_Hive.SetOwnership( null ); // with null user which should never be the case
				Assert.IsFalse( m_Hive.UserOwned, "Null user incorrectly return true" );
			}

			[Test] public void InitializeFeedsTest()
			{
				User user = new User();
				user.Login = "okarim";

				// add global hive to test user
				user.SetHive( m_Hive.ConfigToXml() );

				// try to initialize an empty feed list
				m_Hive.InitializeFeeds( user.GetHive( m_Hive.Guid ), m_TempFolder, null );
				Assert.AreEqual( 0, m_Hive.Feeds.Count, "Unexpected feeds in hive" );

				// add test feed to hive				
				string guid = System.Guid.NewGuid().ToString();
				FeedModel feed = new FeedModel( guid, "http://www.buzm.com", m_TempFolder );

				feed.Placement = "myplacement";
				user.SetFeed( m_Hive.Guid, feed.ConfigToXml() );

				// try to initialize a single valid feed sans name
				m_Hive.InitializeFeeds( user.GetHive( m_Hive.Guid ), m_TempFolder, null );

				Assert.AreEqual( 1, m_Hive.Feeds.Count, "Incorrect count after first feed" );
				Assert.IsTrue( m_Hive.Feeds.Contains( guid ), "Valid feed without name not added" );

				// add another feed with name
				guid = System.Guid.NewGuid().ToString();
				feed = new FeedModel( guid, "http://www.yahoo.com", m_TempFolder );

				feed.Name = "myname";
				user.SetFeed( m_Hive.Guid, feed.ConfigToXml() );

				// try to initialize a single valid feed with name
				m_Hive.InitializeFeeds( user.GetHive( m_Hive.Guid ), m_TempFolder, null );

				Assert.AreEqual( 2, m_Hive.Feeds.Count, "Incorrect count after second feed" );
				Assert.IsTrue( m_Hive.Feeds.Contains( guid ), "Valid feed with name not added" );

				// add valid feed channel to hive
				ChannelType validChannel = new ChannelType();
				validChannel.Guid = guid; // valid feed guid				
				m_Hive.AddContent( validChannel.ToXml() );

				// add invalid feed channel to hive
				ChannelType invalidChannel = new ChannelType();
				invalidChannel.Guid = System.Guid.NewGuid().ToString();
				m_Hive.AddContent( invalidChannel.ToXml() );

				// confirm channels were added
				string channelXPath = "/channel/*[guid = '{0}']"; 

				Assert.IsNotNull( m_Hive.m_XmlDoc.SelectSingleNode( String.Format( channelXPath, validChannel.Guid ) ), "Expected valid channel before validate" );
				Assert.IsNotNull( m_Hive.m_XmlDoc.SelectSingleNode( String.Format( channelXPath, invalidChannel.Guid ) ), "Expected invalid channel before validate" );

				// try to initialize and validate feeds
				m_Hive.InitializeFeeds( user.GetHive( m_Hive.Guid ), m_TempFolder, null );

				Assert.IsNotNull( m_Hive.m_XmlDoc.SelectSingleNode( String.Format( channelXPath, validChannel.Guid ) ), "Expected valid channel after validate" );
				Assert.IsNull( m_Hive.m_XmlDoc.SelectSingleNode( String.Format( channelXPath, invalidChannel.Guid ) ), "Unexpected invalid channel after validate" );
			}

			[Test] public void InitializeMembersTest()
			{
				User user = new User();
				user.Login = "okarim";

				// add global hive to test user
				user.SetHive( m_Hive.ConfigToXml() );

				// try to initialize an empty member list
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.AreEqual( 0, m_Hive.Members.Count, "Unexpected members in hive" );

				// add test member to hive
				UserConfigType memUser = new UserConfigType();
				memUser.Guid = "memGuid1";								
				user.SetMember( m_Hive.Guid, memUser.ToXml() );

				// try to initialize hive with a single invalid member
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.AreEqual( 0, m_Hive.Members.Count, "Invalid member added sans login & email " );

				// reset member with login specified
				user.RemoveMember( m_Hive.Guid, memUser.Guid );
				memUser.Login = "memUser1";
				user.SetMember( m_Hive.Guid, memUser.ToXml() );

				// try to initialize hive with a single valid member
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.IsTrue( m_Hive.Members.Contains( memUser.Guid ), "Valid member with login not added" );

				// create member with email specified
				memUser = new UserConfigType();
				memUser.Guid = "memGuid2";
				memUser.Email = "mem2@buzm.com";
				user.SetMember( m_Hive.Guid, memUser.ToXml() );

				// try to initialize hive with one new and one existing member
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.IsTrue( m_Hive.Members.Contains( memUser.Guid ), "Valid member with email not added" );
				
				// add member with same login as user
				memUser = new UserConfigType();
				memUser.Guid = "memGuid3";
				memUser.Login = user.Login;
				user.SetMember( m_Hive.Guid, memUser.ToXml() );

				// try to initialize with hive user in the member list
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.IsFalse( m_Hive.Members.Contains( memUser.Guid ), "Hive user added as member" );

				// create member with User object instead of UserConfigType
				User memUser4 = new User();
				memUser4.Guid = "memGuid4";
				memUser4.Login = "memUser4";
				user.SetMember( m_Hive.Guid, memUser4.ToXmlString() );

				// try to initialize hive with member created from User object
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.IsTrue( m_Hive.Members.Contains( memUser4.Guid ), "Member created from User object not added" );
				
				// confirm total member count to this point
				Assert.AreEqual( 3, m_Hive.Members.Count, "Incorrect member count after multiple additions" );

				// reset user and hive
				user = user.CloneIdentity();
				user.SetHive( m_Hive.ConfigToXml() );
				m_Hive.Members.Clear();

				// reset state for memUser 
				memUser = new UserConfigType();
				memUser.Guid = "memGuid1";
				
				// add a member and clear the guid node
				XmlNode memNode = user.SetMember( m_Hive.Guid, memUser.ToXml() );
				XmlNode guidNode = memNode.SelectSingleNode( "guid" );								
				guidNode.InnerText = String.Empty;

				// try to initialize with an empty guid node
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.AreEqual( 0, m_Hive.Members.Count, "Added member with empty guid" );

				memNode.RemoveAll(); // try to initialize with missing guid node
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.AreEqual( 0, m_Hive.Members.Count, "Added member with missing guid" );

				// extract members node for the global hive
				XmlNode hiveNode = user.GetHive( m_Hive.Guid );
				XmlNode membersNode = hiveNode.SelectSingleNode( "members" );

				// test membership with legacy xml string
				membersNode.InnerXml = "<member><name>omar</name></member>"; 
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.AreEqual( 0, m_Hive.Members.Count, "Added member with legacy xml" );

				hiveNode.RemoveChild( membersNode ); // remove members node 
				m_Hive.InitializeMembers( user.GetHive( m_Hive.Guid ), user, null );
				Assert.AreEqual( 0, m_Hive.Members.Count, "Added member without members node" );

				m_Hive.InitializeMembers( null, user, null ); // test with null hiveNode
				Assert.AreEqual( 0, m_Hive.Members.Count, "Added member with null hive node" );
			}
		}

		#endif
		#endregion
	}
}
