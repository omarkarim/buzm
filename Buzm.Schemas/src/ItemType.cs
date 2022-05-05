using System;
using System.Xml;
using System.Xml.Serialization;
using Buzm.Schemas.Sharing;
using NUnit.Framework;
using Buzm.Utility;

namespace Buzm.Schemas
{
	[XmlRootAttribute( "item", Namespace="", IsNullable=false )]
	public class ItemType : ContentType
	{
		private string m_Hash;
		private SyncType m_Sync;
		private AuthorType m_Author;

		public ItemType(){ } // deserialize
		public ItemType( string authorLogin )
		{
			m_Author = new AuthorType();
			m_Author.Login = authorLogin;
			
			m_Sync = new SyncType( Guid, Modified, authorLogin );
			m_Hash = m_Sync.GetHash( String.Empty );
		}

		public void AddVersion( string by )
		{
			DateTime now = DateTime.Now;
			Modified = now; // set modified time
			Expires = now; // set expiration time

			if( m_Sync != null ) m_Sync.AddVersion( now, by );
			else m_Sync = new SyncType( Guid, now, by );
			m_Hash = m_Sync.GetHash( String.Empty );
		}

		public bool IsNewer( ItemType rival )
		{
			if( rival == null ) return true;
			if( (m_Sync != null) && (rival.Sync != null) )
			{
				if( m_Sync.IsWinner( rival.Sync ) )
				{
					if( !m_Sync.IsConflict( rival.Sync ) )
						return true; // clear winner
				}
				else if( !rival.Sync.IsConflict( m_Sync ) )					
					return false; // not newer
			}
			return Modified > rival.Modified;
		}

		public void SetDeleted( string by )
		{			
			Link = null;
			Tags = null;

			Summary = null;
			Position = null;

			AddVersion( by );
			m_Sync.Deleted = true;
		}

		[XmlElement("author")] public AuthorType Author
		{ get { return m_Author; } set { m_Author = value; } }

		[XmlElement( "hash" )] public string Hash
		{ get { return m_Hash; } set { m_Hash = value; } }

		[XmlElement( "sync", Namespace = "http://www.microsoft.com/schemas/rss/sse" )]
		public SyncType Sync { get { return m_Sync; } set { m_Sync = value; } }

		public override XmlSerializerNamespaces GetNamespaces()
		{
			XmlSerializerNamespaces ns = base.GetNamespaces();
			ns.Add( "sx", "http://www.microsoft.com/schemas/rss/sse" );
			return ns; // extend base namespaces
		}

		public static ItemType FromXml( string xml )
		{
			// deserialize xml to create item type
			return BaseType.FromXml<ItemType>( xml );
		}

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class ItemTypeTest
		{
			[SetUp] public void SetUp(){ }
			[TearDown] public void TearDown() { }
			
			[Test] public void RoundtripTest()
			{ 							
				// setup a reference date for posts
				DateTime customPostTime = DateTime.Now;

				ItemType itemOne = new ItemType();
				itemOne.Guid = "\x000bitem guid 1";					
				itemOne.Title = "<![CDATA[item <title> 1]]>";
				
				itemOne.Link = "http://www.buzm.com?item&one";
				itemOne.Tags = "\x0001<>\"&' buzm\" <tag />]]>ön";
				itemOne.Summary = ">&#160;&amp;#169;&lt;&gt;&quot;&amp;&apos;&#x27;<";

				itemOne.Modified = customPostTime.AddDays(1);
				itemOne.Expires = customPostTime;
				itemOne.Posted = customPostTime;				
				
				itemOne.SetMaxExpireDate();
				itemOne.Position = "2,1";
				itemOne.Priority = "75";
				
				AuthorType authorOne = new AuthorType();
				authorOne.Login = "okarim";
				itemOne.Author = authorOne;

				// serialize item to string
				string itemXml = itemOne.ToXml();							
				SafeXmlDoc itemXmlDoc = new SafeXmlDoc( itemXml );

				ItemType itemFromXml = ItemType.FromXml( itemXml );
				Assert.IsNotNull( itemFromXml, "Could not deserialize item xml" );
				
				// compare serialized+deserialized values to original
				ValidateItem( itemFromXml, itemXmlDoc, customPostTime );

				// reserialize created item
				itemXml = itemFromXml.ToXml();
				itemXmlDoc = new SafeXmlDoc( itemXml );

				ItemType itemFromXmlAgain = ItemType.FromXml( itemXml );
				Assert.IsNotNull( itemFromXmlAgain, "Could not deserialize item xml again" );

				// compare serialized+deserialized values once again
				ValidateItem( itemFromXmlAgain, itemXmlDoc, customPostTime );												
			}

			private void ValidateItem( ItemType itemFromXml, SafeXmlDoc itemXmlDoc, DateTime customPostTime )
			{
				XmlNode cdataTitleNode = itemXmlDoc.SelectSingleNode( "/item/title" );
				Assert.AreEqual( "<![CDATA[item <title> 1]]>", cdataTitleNode.InnerXml, "Got incorrect item title cdata" );

				Assert.AreEqual( "item guid 1", itemXmlDoc.GetInnerText( "/item/guid", "" ), "Got incorrect item guid text" );
				Assert.AreEqual( "item guid 1", itemFromXml.Guid, "Got incorrect item guid" );

				Assert.AreEqual( "item <title> 1", itemXmlDoc.GetInnerText( "/item/title", "" ), "Got incorrect item title text" );
				Assert.AreEqual( "item <title> 1", itemFromXml.Title, "Got incorrect item title" );

				Assert.AreEqual( "<>\"&' buzm\" <tag />ön", itemXmlDoc.GetInnerText( "/item/tags", "" ), "Got incorrect item tags text" );
				Assert.AreEqual( "<>\"&' buzm\" <tag />ön", itemFromXml.Tags, "Got incorrect item tags" );

				Assert.AreEqual( "http://www.buzm.com?item&one", itemXmlDoc.GetInnerText( "/item/link", "" ), "Got incorrect item link text" );
				Assert.AreEqual( "http://www.buzm.com?item&one", itemFromXml.Link, "Got incorrect item link" );

				Assert.AreEqual( ">&#160;&amp;#169;&lt;&gt;&quot;&amp;&apos;&#x27;<", itemXmlDoc.GetInnerText( "/item/summary", "" ), "Got incorrect item summary text" );
				Assert.AreEqual( ">&#160;&amp;#169;&lt;&gt;&quot;&amp;&apos;&#x27;<", itemFromXml.Summary, "Got incorrect item summary" );

				Assert.AreEqual( Format.DateToString( customPostTime ), itemXmlDoc.GetInnerText( "/item/posted", "" ), "Got incorrect item posted text" );
				Assert.AreEqual( customPostTime.Date, itemFromXml.Posted.Date, "Got incorrect item posted date" );

				Assert.AreEqual( Format.DateToString( customPostTime.AddDays( 1 ) ), itemXmlDoc.GetInnerText( "/item/modified", "" ), "Got incorrect item modified text" );
				Assert.AreEqual( customPostTime.AddDays( 1 ).Date, itemFromXml.Modified.Date, "Got incorrect item modified date" );

				Assert.AreEqual( Format.DateToString( customPostTime.AddYears( 100 ) ), itemXmlDoc.GetInnerText( "/item/expires", "" ), "Got incorrect max expire text" );
				Assert.AreEqual( customPostTime.AddYears( 100 ).Date, itemFromXml.Expires.Date, "Got incorrect item expires date" );

				Assert.AreEqual( "2,1", itemXmlDoc.GetInnerText( "/item/position", "" ), "Got incorrect item position text" );
				Assert.AreEqual( "2,1", itemFromXml.Position, "Got incorrect item position" );

				Assert.AreEqual( "75", itemXmlDoc.GetInnerText( "/item/priority", "" ), "Got incorrect item priority text" );
				Assert.AreEqual( "75", itemFromXml.Priority, "Got incorrect item priority" );

				Assert.AreEqual( "okarim", itemXmlDoc.GetInnerText( "/item/author/login", "" ), "Got incorrect item author text" );
				Assert.AreEqual( "okarim", itemFromXml.Author.Login, "Got incorrect item author" );			
			}

			[Test] public void SharingTest()
			{
				ItemType item = new ItemType( "okarim1" );				
				item.Title = "this is my item title";
				
				string itemXml = item.ToXml(); // serialize				
				SafeXmlDoc itemXmlDoc = new SafeXmlDoc( itemXml );

				ItemType itemFromXml = ItemType.FromXml( itemXml );
				ValidateSharing( item, itemFromXml, itemXmlDoc, 1, "okarim1" );

				// invalidate item dates
				item.Modified = DateTime.MinValue;
				item.Expires = DateTime.MinValue;
				item.Posted = DateTime.MinValue;
								
				// add two versions to item
				item.AddVersion( "okarim2" );
				item.AddVersion( "okarim3" );

				Assert.AreNotEqual( DateTime.MinValue, item.Modified, "Expected Modified to be updated" );
				Assert.AreNotEqual( DateTime.MinValue, item.Expires, "Expected Expires to be updated" );
				Assert.AreEqual( DateTime.MinValue, item.Posted, "Expected Posted to not be updated" );
				
				itemXml = item.ToXml(); // serialize
				itemXmlDoc = new SafeXmlDoc( itemXml );
				itemFromXml = ItemType.FromXml( itemXml );

				ValidateSharing( item, itemFromXml, itemXmlDoc, 3, "okarim3" );
				Assert.AreEqual( 2, itemFromXml.Sync.History.Updates.Count, "Expected 2 updates" );

				UpdateType update = (UpdateType)itemFromXml.Sync.History.Updates[0];
				Assert.AreEqual( item.Modified.Date, update.When.Date, "Expected When to be modified time for first update" );
				Assert.AreEqual( "okarim2", update.By, "Expected By to be 'okarim2' for first update" );

				update = (UpdateType)itemFromXml.Sync.History.Updates[1];
				Assert.AreEqual( item.Modified.Date, update.When.Date, "Expected When to be modified time for second update" );
				Assert.AreEqual( "okarim1", update.By, "Expected By to be 'okarim1' for second update" );

				// invalidate item history
				item.Sync.History = null;
				item.Sync.Conflict = true;
				
				// add another version
				item.AddVersion( "okarim4" );

				itemXml = item.ToXml(); // serialize
				itemXmlDoc = new SafeXmlDoc( itemXml );
				itemFromXml = ItemType.FromXml( itemXml );

				ValidateSharing( item, itemFromXml, itemXmlDoc, 4, "okarim4" );
				Assert.AreEqual( 0, itemFromXml.Sync.History.Updates.Count, "Expected 0 updates after history was invalidated" );
				Assert.IsFalse( itemFromXml.Sync.Conflict, "Expected conflict to be false after adding a version" );

				// populate additional metadata
				item.Link = "http://www.buzm.com";
				item.Tags = "tagOne tagTwo tagThree";
				item.Summary = "here is some summary text...";
				item.Position = "random position";

				// set the item to deleted
				item.SetDeleted( "okarim5" );

				itemXml = item.ToXml(); // serialize
				itemXmlDoc = new SafeXmlDoc( itemXml );
				itemFromXml = ItemType.FromXml( itemXml );

				ValidateSharing( item, itemFromXml, itemXmlDoc, 5, "okarim5" );
				Assert.IsTrue( itemFromXml.Sync.Deleted, "Expected deleted to be true after roundtrip" );
				Assert.AreEqual( 1, itemFromXml.Sync.History.Updates.Count, "Expected delete to be counted as update" );
				
				Assert.IsNull( itemFromXml.Link, "Expected null Link after delete" );
				Assert.IsNull( itemFromXml.Tags, "Expected null Tags after delete" );

				Assert.IsNull( itemFromXml.Summary, "Expected null Summary after delete" );
				Assert.IsNull( itemFromXml.Position, "Expected null Position after delete" );

				// invalidate item sync
				item.Sync = null;

				// add another version
				item.AddVersion( "manavi" );

				itemXml = item.ToXml(); // serialize
				itemXmlDoc = new SafeXmlDoc( itemXml );
				itemFromXml = ItemType.FromXml( itemXml );

				ValidateSharing( item, itemFromXml, itemXmlDoc, 1, "manavi" );
				Assert.AreEqual( 0, itemFromXml.Sync.History.Updates.Count, "Expected 0 updates after sync was invalidated" );
			}

			private void ValidateSharing( ItemType item, ItemType itemFromXml, SafeXmlDoc itemXmlDoc, int version, string author )
			{
				Assert.IsNotNull( itemFromXml, "Could not deserialize item xml" );

				XmlNamespaceManager nsmgr = new XmlNamespaceManager( itemXmlDoc.NameTable );
				nsmgr.AddNamespace( "sx", "http://www.microsoft.com/schemas/rss/sse" );

				XmlNode sxNamespaceNode = itemXmlDoc.SelectSingleNode( "/item", nsmgr ).Attributes.GetNamedItem( "sx", "http://www.w3.org/2000/xmlns/" );
				Assert.AreEqual( "http://www.microsoft.com/schemas/rss/sse", sxNamespaceNode.InnerText, "Incorrect sx/sse namespace after serialize" );

				Assert.AreEqual( item.Guid, itemXmlDoc.GetInnerText( "/item/guid", "" ), "Got incorrect item guid text" );
				Assert.AreEqual( item.Guid, itemFromXml.Guid, "Got incorrect item guid" );

				Assert.AreEqual( "this is my item title", itemXmlDoc.GetInnerText( "/item/title", "" ), "Got incorrect item title text" );
				Assert.AreEqual( "this is my item title", itemFromXml.Title, "Got incorrect item title" );

				Assert.AreEqual( item.Guid, itemXmlDoc.SelectSingleNode( "/item/sx:sync/@id", nsmgr ).InnerXml, "Incorrect id xml after serialize" );
				Assert.AreEqual( item.Guid, itemFromXml.Sync.Guid, "Incorrect id after deserialize" );

				Assert.AreEqual( version.ToString(), itemXmlDoc.SelectSingleNode( "/item/sx:sync/@version", nsmgr ).InnerXml, "Incorrect version xml after serialize" );
				Assert.AreEqual( version, itemFromXml.Sync.Version, "Incorrect version after deserialize" );

				Assert.AreEqual( item.Sync.History.WhenString, itemXmlDoc.SelectSingleNode( "/item/sx:sync/sx:history/@when", nsmgr ).InnerXml, "Incorrect history/when xml after serialize" );
				Assert.AreEqual( item.Modified.Date, itemFromXml.Sync.History.When.Date, "Incorrect history/when date after deserialize" );

				Assert.AreEqual( author, itemXmlDoc.SelectSingleNode( "/item/sx:sync/sx:history/@by", nsmgr ).InnerXml, "Incorrect history/by xml after serialize" );
				Assert.AreEqual( author, itemFromXml.Sync.History.By, "Incorrect history/by value after deserialize" );
			}
		}

		#endif
		#endregion
	}
}
