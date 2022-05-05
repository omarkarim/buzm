using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Buzm.Utility.Algorithms;
using System.Diagnostics;
using NUnit.Framework;
using Buzm.Utility;
using System.Text;

namespace Buzm.Schemas.Sharing
{
	[XmlRootAttribute( "sync", Namespace = "http://www.microsoft.com/schemas/rss/sse" )]
	public class SyncType : BaseType
	{
		private string m_Guid;
		private int m_Version;

		private bool m_Deleted;
		private bool m_Conflict;

		// history contains updates
		private HistoryType m_History;

		public SyncType() { } // required by serializer
		public SyncType( string guid, DateTime when, string by )
		{
			m_Guid = guid;
			AddVersion( when, by );
		}

		public void AddVersion( DateTime when, string by )
		{
			m_Conflict = false; // reset conflict
			m_Version++; // increment version

			if( m_History == null )
			{
				m_History = new HistoryType();
				m_History.When = when;
				m_History.By = by;
			}
			else m_History.AddUpdate( when, by );
		}

		public bool IsWinner( SyncType rival )
		{
			if( rival == null )
				return true;
			else if( m_Version > rival.Version )
				return true;
			else if( m_Version < rival.Version )
				return false;
			else if( m_History == null )
				return false;
			else if( rival.History == null )
				return true;
			else if( m_History.When > rival.History.When )
				return true;
			else if( m_History.When < rival.History.When )
				return false;
			else if( String.Compare( m_History.By, rival.History.By,
				StringComparison.OrdinalIgnoreCase ) > 0 )
				return true;
			else return false; // tie or loser
		}

		public bool IsConflict( SyncType loser )
		{			
			if( ( (loser != null) && (loser.History != null) )
				&& (m_History != null) ) // history exists
			{
				int delta = m_Version - loser.Version;
				if( (delta > 0) && (m_History.Updates != null) )
				{
					if( delta <= m_History.Updates.Count )
					{
						UpdateType up = m_History.Updates[delta - 1] as UpdateType;
						if( up != null ) return !up.IsMatch( loser.History );
					}
				}
				else if( delta == 0 ) // same version
					return !m_History.IsMatch( loser.History );
				else if( delta < 0 ) return true;
			}
			return false; // assume no conflict
		}

		public bool SetConflict( bool conflict, SyncType rival )
		{
			if( conflict ) // set if true, otherwise preserve
			{
				m_Conflict = conflict;
				if( rival != null ) rival.Conflict = conflict;
			}
			return conflict; // for convenience
		}

		public string GetHash( string salt )
		{
			try // to calculate leaf hash for sync data
			{
				string data = salt + m_Guid + m_Version; // collation data
				if( m_History != null ) data += m_History.WhenString + m_History.By;

				if( !String.IsNullOrEmpty( data ) ) // valid sync data
				{
					Encoding utf8Encoder = Encoding.UTF8;
					byte[] dataBytes = utf8Encoder.GetBytes( data );

					HashTree hashTree = new HashTree(); // merkle tree
					byte[] hashBytes = hashTree.GetLeafHash( dataBytes );
					return Convert.ToBase64String( hashBytes );
				}
			}
			catch( Exception e )
			{
				Log.Write( "Failed to create leaf hash for sync",
				TraceLevel.Warning, "SyncType.GetHash", e );
			}
			return null; // no hash created
		}

		public static SyncType FromXml( string xml )
		{
			// deserialize xml to create sync type
			return BaseType.FromXml<SyncType>( xml );
		}

		public override XmlSerializerNamespaces GetNamespaces()
		{
			XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
			ns.Add( "sx", "http://www.microsoft.com/schemas/rss/sse" );
			return ns; // ignore base namespaces
		}

		#region Serialized Properties

		[XmlAttribute( "id" )] public string Guid
		{ get { return m_Guid; } set { m_Guid = value; } }

		[XmlAttribute( "version" )] public int Version
		{ get { return m_Version; } set { m_Version = value; } }

		[XmlAttribute( "deleted" )] public string DeletedString
		{
			get { return Format.BooleanToString( m_Deleted ); }
			set { Boolean.TryParse( value, out m_Deleted ); }
		}

		[XmlAttribute( "conflict" )] public string ConflictString
		{
			get { return Format.BooleanToString( m_Conflict ); }
			set { Boolean.TryParse( value, out m_Conflict ); }
		}

		[XmlElement( "history" )] public HistoryType History
		{ get { return m_History; } set { m_History = value; } }

		# endregion

		#region Unserialized Properties

		[XmlIgnore] public bool Deleted { get { return m_Deleted; } set { m_Deleted = value; } }
		[XmlIgnore] public bool Conflict { get { return m_Conflict; } set { m_Conflict = value; } }

		# endregion
		
		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class SyncTypeTest
		{
			[SetUp] public void SetUp() { }
			[TearDown] public void TearDown() { }

			[Test] public void RoundtripTest()
			{
				// create a reference date to measure results against			
				DateTime referenceDate = new DateTime( 2007, 6, 7, 14, 12, 6 );
				
				// setup a complete sync tree with update history
				SyncType sync = new SyncType();
				sync.Guid = "&#x1;sync <p> guid";
				
				sync.Deleted = true;
				sync.Version = 5;

				HistoryType history = new HistoryType();
				history.By = "<homepc::\"okarim\">";
				history.When = referenceDate;

				// 1 - add empty update
				UpdateType update = new UpdateType();
				history.Updates.Add( update );

				// 2 - add empty by without date
				update = new UpdateType();
				update.By = String.Empty;
				history.Updates.Add( update );

				// 3 - add date-only update
				update = new UpdateType();
				update.When = referenceDate;
				history.Updates.Add( update );				

				// 4 - add complete update
				update = new UpdateType();
				update.When = referenceDate;
				update.By = "updatebyomarkarim";				
				history.Updates.Add( update );

				sync.History = history;
				string syncXml = sync.ToXml();
				
				SafeXmlDoc syncXmlDoc = new SafeXmlDoc( syncXml );
				SyncType syncTypeFromXml = SyncType.FromXml( syncXml );

				XmlNamespaceManager nsmgr = new XmlNamespaceManager( syncXmlDoc.NameTable );
				nsmgr.AddNamespace( "sx", "http://www.microsoft.com/schemas/rss/sse" );				
				
				// validate sync node				
				Assert.IsNotNull( syncXmlDoc.SelectSingleNode( "/sx:sync", nsmgr ), "No sync node after serialize" );
				Assert.IsNotNull( syncTypeFromXml, "Could not deserialize sync xml" );

				XmlNode sxNamespaceNode = syncXmlDoc.SelectSingleNode( "/sx:sync", nsmgr ).Attributes.GetNamedItem( "sx", "http://www.w3.org/2000/xmlns/" );
				Assert.AreEqual( "http://www.microsoft.com/schemas/rss/sse", sxNamespaceNode.InnerText, "Incorrect sx/sse namespace after serialize" );

				Assert.AreEqual( "&amp;#x1;sync &lt;p&gt; guid", syncXmlDoc.SelectSingleNode( "/sx:sync/@id", nsmgr ).InnerXml, "Incorrect id xml after serialize" );
				Assert.AreEqual( "&#x1;sync <p> guid", syncTypeFromXml.Guid, "Incorrect id after deserialize" );

				Assert.AreEqual( "5", syncXmlDoc.SelectSingleNode( "/sx:sync/@version", nsmgr ).InnerXml, "Incorrect version xml after serialize" );
				Assert.AreEqual( 5, syncTypeFromXml.Version, "Incorrect version after deserialize" );

				Assert.AreEqual( "true", syncXmlDoc.SelectSingleNode( "/sx:sync/@deleted", nsmgr ).InnerXml, "Incorrect delete xml after serialize" );
				Assert.IsTrue( syncTypeFromXml.Deleted, "Incorrect delete value after deserialize" );

				Assert.IsNull( syncXmlDoc.SelectSingleNode( "/sx:sync/@conflict", nsmgr ), "Unexpected conflict node after serialize" );
				Assert.IsFalse( syncTypeFromXml.Conflict, "Incorrect conflict value after deserialize" );

				// validate history node
				Assert.AreEqual( "Thu, 07 Jun 2007 18:12:06 GMT", syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/@when", nsmgr ).InnerXml, "Incorrect history/when xml after serialize" );
				Assert.AreEqual( referenceDate, syncTypeFromXml.History.When, "Incorrect history/when date after deserialize" );

				Assert.AreEqual( "&lt;homepc::\"okarim\"&gt;", syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/@by", nsmgr ).InnerXml, "Incorrect history/by xml after serialize" );
				Assert.AreEqual( "<homepc::\"okarim\">", syncTypeFromXml.History.By, "Incorrect history/by value after deserialize" );

				// validate update node one
				Assert.AreEqual( "Mon, 01 Jan 0001 05:00:00 GMT", syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/sx:update[1]/@when", nsmgr ).InnerXml, "Expected update/when MinDate after serialize" );
				Assert.AreEqual( DateTime.MinValue, ((UpdateType)syncTypeFromXml.History.Updates[0]).When, "Expected update/when MinDate after deserialize" );

				Assert.IsNull( syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/sx:update[1]/@by", nsmgr ), "Unexpected update/by node after serialize" );
				Assert.IsNull( ((UpdateType)syncTypeFromXml.History.Updates[0]).By, "Unexpected update/by value after deserialize" );

				// validate update node two
				Assert.AreEqual( "Mon, 01 Jan 0001 05:00:00 GMT", syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/sx:update[2]/@when", nsmgr ).InnerXml, "Expected update/when MinDate after serialize" );
				Assert.AreEqual( DateTime.MinValue, ((UpdateType)syncTypeFromXml.History.Updates[1]).When, "Expected update/when MinDate after deserialize" );

				Assert.AreEqual( String.Empty, syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/sx:update[2]/@by", nsmgr ).InnerXml, "Expected empty update/by node after serialize" );
				Assert.AreEqual( String.Empty, ((UpdateType)syncTypeFromXml.History.Updates[1]).By, "Expected empty update/by value after deserialize" );

				// validate update node three
				Assert.AreEqual( "Thu, 07 Jun 2007 18:12:06 GMT", syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/sx:update[3]/@when", nsmgr ).InnerXml, "Incorrect update/when date after serialize" );
				Assert.AreEqual( referenceDate, ((UpdateType)syncTypeFromXml.History.Updates[2]).When, "Incorrect update/when date after deserialize" );

				Assert.IsNull( syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/sx:update[3]/@by", nsmgr ), "Unexpected update/by node after serialize" );
				Assert.IsNull( ((UpdateType)syncTypeFromXml.History.Updates[2]).By, "Unexpected update/by value after deserialize" );

				// validate update node four
				Assert.AreEqual( "Thu, 07 Jun 2007 18:12:06 GMT", syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/sx:update[4]/@when", nsmgr ).InnerXml, "Incorrect update/when date after serialize" );
				Assert.AreEqual( referenceDate, ((UpdateType)syncTypeFromXml.History.Updates[3]).When, "Incorrect update/when date after deserialize" );

				Assert.AreEqual( "updatebyomarkarim", syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/sx:update[4]/@by", nsmgr ).InnerXml, "Incorrect update/by xml after serialize" );
				Assert.AreEqual( "updatebyomarkarim", ((UpdateType)syncTypeFromXml.History.Updates[3]).By, "Incorrect update/by value after deserialize" );

				// setup alternative sync configuration
				sync.Guid = null;
				sync.History.By = null;
				sync.History.Updates = null;
				sync.Conflict = true;
				sync.Deleted = false;

				// serialize sync node again
				syncXml = sync.ToXml();
				syncXmlDoc = new SafeXmlDoc( syncXml );
				
				// invalidate history/when value
				XmlNode historyWhen = syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/@when", nsmgr );
				historyWhen.InnerText = "blah de blah";
				
				// deserialize sync xml with invalid date
				syncTypeFromXml = SyncType.FromXml( syncXmlDoc.OuterXml );
				Assert.IsNotNull( syncTypeFromXml, "Could not deserialize sync xml with invalid date" );

				Assert.IsNull( syncXmlDoc.SelectSingleNode( "/sx:sync/@id", nsmgr ), "Unexpected id node after serialize" );
				Assert.IsNull( syncTypeFromXml.Guid, "Unexpected id after deserialize" );

				Assert.IsNull( syncXmlDoc.SelectSingleNode( "/sx:sync/@deleted", nsmgr ), "Unexpected deleted node after serialize" );
				Assert.IsFalse( syncTypeFromXml.Deleted, "Incorrect deleted value after deserialize" );

				Assert.AreEqual( "true", syncXmlDoc.SelectSingleNode( "/sx:sync/@conflict", nsmgr ).InnerXml, "Incorrect conflict xml after serialize" );
				Assert.IsTrue( syncTypeFromXml.Conflict, "Incorrect conflict value after deserialize" );

				Assert.AreEqual( "blah de blah", syncXmlDoc.SelectSingleNode( "/sx:sync/sx:history/@when", nsmgr ).InnerXml, "Expected corrupt history/when date" );
				Assert.AreEqual( DateTime.MinValue, syncTypeFromXml.History.When, "Expected history/when MinDate after deserialize" );

				// invalidate version value
				XmlNode syncVersion = syncXmlDoc.SelectSingleNode( "/sx:sync/@version", nsmgr );
				syncVersion.InnerText = "also blah de blah";

				// deserialize sync xml with invalid version
				syncTypeFromXml = SyncType.FromXml( syncXmlDoc.OuterXml );
				Assert.IsNull( syncTypeFromXml, "Sync type deserialized from invalid version" );
			}
		}

		#endif
		#endregion
	}
}
