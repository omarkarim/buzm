using System;
using System.IO;
using System.Net;
using System.Web;
using System.Xml;
using Buzm.Schemas;
using Buzm.Utility;
using NUnit.Framework;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace Buzm.Network.Feeds
{
	public class FeedModel
	{
		private string m_Url;
		private string m_File;
		private string m_Guid;
		private string m_Name;
		private int m_MaxItems;
		private string m_HiveGuid;
		private string m_Placement;

		// feed content vars
		private int m_MinsToLive;
		private DateTime m_LastUpdatedTime;	
		private SafeXmlDoc m_ContentXmlDoc;
		private static readonly string m_UserAgent;
		
		// feed configuration constants		
		private const int DEFAULT_TTL = 90;
		private const int DEFAULT_MAX_ITEMS = 5;

		private const string FEED_FOLDER = @"Feeds/";
		private const string NAME_REGEX = @"[\[\]\|\s]+";

		static FeedModel()
		{
			m_UserAgent = Application.ProductName + "/" 
						+ Application.ProductVersion;
		}

		public FeedModel( string guid, string url, string dataFolder )
		{	
			m_Name = "";
			m_Url  = url;
			m_Guid = guid;			

			// set default feed properties
			m_MaxItems = Config.GetIntValue( "preferences/feeds/maxItems", DEFAULT_MAX_ITEMS );
			m_MinsToLive = Config.GetIntValue( "preferences/feeds/defaultTTL", DEFAULT_TTL );

			// configure folder and filename for feed storage
			string feedFolder = FileUtils.AppendSeparator( dataFolder ) + FEED_FOLDER;
			m_File = FileUtils.CreateFolder( feedFolder ) + FileUtils.GuidToFileName( m_Guid );
			
			// try loading RSS feed from disk
			m_ContentXmlDoc = new SafeXmlDoc();
			if( File.Exists( m_File ) ) // cached
			{
				FileInfo info = new FileInfo( m_File );
				m_LastUpdatedTime = info.LastWriteTime; // cache time
				m_ContentXmlDoc.LoadFromFile( m_File, "FeedModel.Init" );
			}
			else m_LastUpdatedTime = DateTime.MinValue; // feed not cached
		}

		// TODO: Revisit since bool may not be 
		// the appropriate treatment for errors
		public bool CheckForUpdates( )
		{
			// if the time to live for the feed has expired
			if( TTLExpired( m_LastUpdatedTime, m_MinsToLive ) )
			{
				try // loading feed xml document from known url
				{
					HttpWebRequest request = (HttpWebRequest)WebRequest.Create( m_Url );
					request.UserAgent = m_UserAgent; // set Buzm as user agent

					if( m_ContentXmlDoc.LoadFromWeb( request, "FeedModel " + m_Url ) )
					{
						m_ContentXmlDoc.Save( m_File );
						m_LastUpdatedTime = DateTime.Now;
						return true;
					}
				}
				catch( Exception e )
				{
					// TODO: Surface error to the user after x failures
					// and disable feed or the errors will keep adding up
					// Also handle network disconnected condition properly
					Log.Write( "Could not update feed content: " + m_Url,
					TraceLevel.Info, "FeedModel.CheckForUpdates", e );
				}
			}
			return false;
		}

		private bool TTLExpired( DateTime lastUpdate, int minsToLive )
		{
			TimeSpan diff = DateTime.Now.Subtract( lastUpdate );	
			if( diff.TotalMinutes > minsToLive ) return true;
			else return false;
		}

		public string ToXml()
		{
			// if content document has been populated
			if( m_ContentXmlDoc.DocumentElement != null )
			{
				try // loading channel type from feed xml
				{	
					ChannelType channel = new ChannelType();
					channel.Guid = m_Guid; // populate feed guid
					channel.Position = m_Placement; // set channel layout
					
					RssToChannel( channel ); // only rss is supported currently
					return channel.ToXml(); // return serialized xml from channel
				}
				catch( Exception e )
				{
					Log.Write( "Could not parse feed - " + m_Guid + " : " + m_Url,
					TraceLevel.Warning, "FeedModel.ToXml", e );
				}
			}
			return String.Empty; // return empty string if all else fails
		}

		private void RssToChannel( ChannelType channel )
		{
			ItemType item; // schema type for channel items
			XmlNode itemNode, titleNode, linkNode, summaryNode;

			// extract channel title - null title node should throw an exception since this is the only required element
			string channelTitle = m_ContentXmlDoc.SelectSingleNode( "/*/*[local-name() = 'channel']/*[local-name() = 'title']" ).InnerText;

			if( this.Name == String.Empty ) this.Name = channelTitle; // set feed name if empty
			channel.Title = this.Name; // set channel title to custom feed name or rss title xml
			
			XmlNode channelLinkNode = m_ContentXmlDoc.SelectSingleNode( "/*/*[local-name() = 'channel']/*[local-name() = 'link']" );
			if( channelLinkNode != null ) channel.Link = channelLinkNode.InnerText; // set channel link to rss link text

			// select item nodes with an rss .9x, 1.0 and 2.0 compatible local-name query
			XmlNodeList itemNodes = m_ContentXmlDoc.SelectNodes( "//*[local-name() = 'item']" );

			// loop to add no more than max items to the output channel
			int itemCount = Math.Min( itemNodes.Count, m_MaxItems );
			for( int i=0; i < itemCount; i++ )
			{
				item = new ItemType();
				itemNode = itemNodes[i];
						
				titleNode = itemNode.SelectSingleNode( "*[local-name() = 'title']" );
				if( titleNode != null ) item.Title = titleNode.InnerText;

				linkNode = itemNode.SelectSingleNode( "*[local-name() = 'link']" );
				if( linkNode != null ) item.Link = linkNode.InnerText;

				summaryNode = itemNode.SelectSingleNode( "*[local-name() = 'description']" );
				if( summaryNode != null ) item.Summary = summaryNode.InnerText;

				// add item to channel
				channel.Items.Add( item );
			}
		}

		public string CleanName( string name )
		{
			if( name == null ) return String.Empty;
			
			string newName = Regex.Replace( name, NAME_REGEX, " " );
			return newName.Trim(); // remove trailing spaces
		}

		public string Url
		{
			get { return m_Url; }
			set { m_Url = value; }
		}

		public string Guid
		{
			get { return m_Guid; }
			set { m_Guid = value; }
		}

		public string Name
		{
			get { return m_Name; }
			set { m_Name = CleanName( value ); }
		}

		public string HiveGuid
		{
			get { return m_HiveGuid; }
			set { m_HiveGuid = value; }
		}

		public string Placement
		{
			get { return m_Placement; }
			set { m_Placement = value; }
		}

		/// <summary>Wraps feed config info in an xml 
		/// string based on the config template </summary>
		/// <returns>String containing config xml</returns>
		public string ConfigToXml( )
		{
			// load template for feed properties from config file
			string configXml = Config.GetOuterXml( "templates/config/feed" );
			SafeXmlDoc configXmlDoc = new SafeXmlDoc( configXml ); // load xml
			
			configXmlDoc.SetInnerText( "/feed/url", m_Url, "FeedModel.ConfigToXml" );
			configXmlDoc.SetInnerText( "/feed/name", m_Name, "FeedModel.ConfigToXml" );
			configXmlDoc.SetInnerText( "/feed/guid", m_Guid, "FeedModel.ConfigToXml" );
			configXmlDoc.SetInnerText( "/feed/placement", m_Placement, "FeedModel.ConfigToXml" );
			return configXmlDoc.OuterXml; // with all configuration information populated for feed
		}

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class FeedModelTest
		{
			string m_Guid;			
			string m_TempFile;
			string m_TempFolder;

			private const int MIN_ITEM_COUNT = 3;
			private const string FEED_NAME = " [ Feed | Name ] ";
			private const string FEED_LINK = "http://www.buzm.com";
			
			private string[] m_ItemSummaries = new string[]{ "S1", "S2", "S3" };
			private string[] m_ItemLinks = new string[]{ "http://slashdot.org/article.pl?sid=0&amp;t=1", "L2", "" };
			private string[] m_ItemGuids = new string[]{ "http://slashdot.org/article.pl?sid=0&amp;t=1", "G2", "G3" };
			private string[] m_ItemTitles = new string[]{ "&lt;br /&gt;T1<p><br />0&#36;1.93</p><br />", "<![CDATA[<<T2&#36;1.93<br />]]>", @"<p style=""1"">T3ü</p>" };
		
			[SetUp] public void SetUp() 
			{ 
				// load local config file for this assembly
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly );

				// global feed info for tests to use
				m_Guid = System.Guid.NewGuid().ToString();
				m_TempFolder = FileUtils.CreateTempFolder();
				
				m_TempFile = FileUtils.CreateFolder( m_TempFolder + FEED_FOLDER )
						   + FileUtils.GuidToFileName( m_Guid );
			}

			[TearDown] public void TearDown()
			{ 
				// delete the test folder
				Directory.Delete( m_TempFolder, true );

				// unload configuration or other nunit tests
				Config.UnloadConfig(); // will see it as well
			}

			[Test] public void CleanNameTest()
			{
				// create dummy feed to process
				string tempFolder = Path.GetTempPath();
				FeedModel feed = new FeedModel( "MyGuid", "MyUrl", tempFolder );
	
				feed.Name = null; // set null feed name
				Assert.AreEqual( String.Empty, feed.Name, "Expected empty feed name from null" );

				feed.Name = String.Empty; // set empty feed name
				Assert.AreEqual( String.Empty, feed.Name, "Expected empty feed name from empty" );

				feed.Name = " [ \n| \r\n | ]	" ; // set name with invalid chars
				Assert.AreEqual( String.Empty, feed.Name, "Expected empty feed name from invalid" );

				feed.Name = "\r	One[Wacky Feed]  | \r\n Name\n ";
				Assert.AreEqual( "One Wacky Feed Name", feed.Name, "Expected clean feed name" );
			}

			[Test] public void ConfigToXmlTest()
			{
				// create dummy feed to process
				string tempFolder = Path.GetTempPath();
				FeedModel feed = new FeedModel( "MyGuid", "MyUrl", tempFolder );
				feed.Placement = "MyPlace";
				feed.Name = "MyName";
			
				// create new xml doc from config data and check its values
				SafeXmlDoc configXmlDoc = new SafeXmlDoc( feed.ConfigToXml() );
				Assert.AreEqual( "MyUrl", configXmlDoc.GetInnerText( "/feed/url", "Test" ), "Got incorrect url from feed" );
				Assert.AreEqual( "MyGuid", configXmlDoc.GetInnerText( "/feed/guid", "Test" ), "Got incorrect guid from feed" );
				Assert.AreEqual( "MyName", configXmlDoc.GetInnerText( "/feed/name", "Test" ), "Got incorrect name from feed" );
				Assert.AreEqual( "MyPlace", configXmlDoc.GetInnerText( "/feed/placement", "Test" ), "Got incorrect placement from feed" );
			}

			[Test] public void RSS20ToXmlTest()
			{
				// load sample rss 2.0 document for test
				SafeXmlDoc rssDoc = new SafeXmlDoc( @"<?xml version=""1.0"" ?>
					<rss version=""2.0"">
					<channel>
					<title>" + FEED_NAME + @"</title>
					<link>" + FEED_LINK + @"</link>
					<description>Technology, and the way we do business, is changing the world we know. Wired News is a technology - and business-oriented news service feeding an intelligent, discerning audience. What role does technology play in the day-to-day living of your life? Wired News tells you. How has evolving technology changed the face of the international business world? Wired News puts you in the picture.</description>
					<language>en-us</language>
					<copyright>&#169; Copyright 2004, Lycos, Inc. All Rights Reserved.</copyright>
					<pubDate>Thu, 25 Nov 2004 12:34:48 PST</pubDate>
					<lastBuildDate>Thu, 25 Nov 2004 12:34:48 PST</lastBuildDate>
					<category>Wired News: Top Stories</category>
					<ttl>60</ttl>
					<image>
					<title>Wired News</title>
					<url>http://static.wired.com/news/images/netcenterb.gif</url>
					<link>http://www.wired.com/</link>
					</image>
					<item>
					<title>" + m_ItemTitles[0] + @"</title>
					<link>" + m_ItemLinks[0] + @"</link>
					<guid isPermaLink=""true"">" + m_ItemGuids[0] + @"</guid>
					<description>" + m_ItemSummaries[0] + @"</description>
					<pubDate>Thu, 25 Nov 2004 02:00:00 PST</pubDate>
					</item>
					<item>
					<title>" + m_ItemTitles[1] + @"</title>
					<link>" + m_ItemLinks[1] + @"</link>
					<guid isPermaLink=""true"">" + m_ItemGuids[1] + @"</guid>
					<description>" + m_ItemSummaries[1] + @"</description>
					<pubDate>Thu, 25 Nov 2004 02:00:00 PST</pubDate>
					</item>
					<item>
					<title>" + m_ItemTitles[2] + @"</title>
					<link>" + m_ItemLinks[2] + @"</link>
					<guid isPermaLink=""true"">" + m_ItemGuids[2] + @"</guid>
					<description>" + m_ItemSummaries[2] + @"</description>
					<pubDate>Wed, 24 Nov 2004 12:16:00 PST</pubDate>
					</item>
					<item></item><item></item><item></item>
					</channel>
					</rss>" );
				
				// save document to default location for feed
				rssDoc.SaveToFile( m_TempFile, "RSS20ToXmlTest" );
				FeedToXmlTest( FeedModel.DEFAULT_MAX_ITEMS );
			}

			[Test] public void RSS10ToXmlTest()
			{
				// load sample rss 1.0 document for test
				SafeXmlDoc rssDoc = new SafeXmlDoc( @"<?xml version=""1.0"" encoding=""ISO-8859-1""?>
					<rdf:RDF
					xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
					xmlns=""http://purl.org/rss/1.0/""
					xmlns:dc=""http://purl.org/dc/elements/1.1/""
					xmlns:slash=""http://purl.org/rss/1.0/modules/slash/""
					xmlns:taxo=""http://purl.org/rss/1.0/modules/taxonomy/""
					xmlns:admin=""http://webns.net/mvcb/""
					xmlns:syn=""http://purl.org/rss/1.0/modules/syndication/"">

					<channel rdf:about=""http://slashdot.org/"">
					<title>" + FEED_NAME + @"</title>
					<link>" + FEED_LINK + @"</link>
					<description>News for nerds, stuff that matters</description>
					<dc:language>en-us</dc:language>
					<dc:rights>Copyright 1997-2004, OSTG - Open Source Technology Group, Inc.  All Rights Reserved.</dc:rights>
					<dc:date>2004-11-25T19:41:05+00:00</dc:date>
					<dc:publisher>OSTG</dc:publisher>
					<dc:creator>pater@slashdot.org</dc:creator>
					<dc:subject>Technology</dc:subject>
					<syn:updatePeriod>hourly</syn:updatePeriod>
					<syn:updateFrequency>1</syn:updateFrequency>
					<syn:updateBase>1970-01-01T00:00+00:00</syn:updateBase>
					<items>
					<rdf:Seq>
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/25/1835238&amp;from=rss"" />
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/25/162249&amp;from=rss"" />
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/25/154216&amp;from=rss"" />
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/25/1418248&amp;from=rss"" />
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/25/1416208&amp;from=rss"" />
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/25/1339258&amp;from=rss"" />
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/25/1331227&amp;from=rss"" />
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/25/001208&amp;from=rss"" />
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/24/2338212&amp;from=rss"" />
					<rdf:li rdf:resource=""http://slashdot.org/article.pl?sid=04/11/25/0142228&amp;from=rss"" />
					</rdf:Seq>
					</items>
					<image rdf:resource=""http://images.slashdot.org/topics/topicslashdot.gif"" />
					<textinput rdf:resource=""http://slashdot.org/search.pl"" />
					</channel>

					<image rdf:about=""http://images.slashdot.org/topics/topicslashdot.gif"">
					<title>Slashdot</title>
					<url>http://images.slashdot.org/topics/topicslashdot.gif</url>
					<link>http://slashdot.org/</link>
					</image>

					<item rdf:about=""http://slashdot.org/article.pl?sid=04/11/25/1835238&amp;from=rss"">
					<title>" + m_ItemTitles[0] + @"</title>
					<link>" + m_ItemLinks[0] + @"</link>
					<description>" + m_ItemSummaries[0] + @"</description>
					<dc:creator>timothy</dc:creator>
					<dc:subject>privacy</dc:subject>
					<dc:date>2004-11-25T19:30:00+00:00</dc:date>
					<slash:section>yro</slash:section>
					<slash:department>mens-rea</slash:department>
					<slash:comments>18</slash:comments>
					<slash:hitparade>18,16,9,4,2,1,1</slash:hitparade>
					</item>

					<item rdf:about=""http://slashdot.org/article.pl?sid=04/11/25/162249&amp;from=rss"">
					<title>" + m_ItemTitles[1] + @"</title>
					<link>" + m_ItemLinks[1] + @"</link>
					<description>" + m_ItemSummaries[1] + @"</description>
					<dc:creator>michael</dc:creator>
					<dc:subject>usa</dc:subject>
					<dc:date>2004-11-25T18:35:00+00:00</dc:date>
					<slash:section>mainpage</slash:section>
					<slash:department>stop-reading-slashdot,-spend-time-with-family</slash:department>
					<slash:comments>58</slash:comments>
					<slash:hitparade>58,52,38,21,6,2,0</slash:hitparade>
					</item>

					<item rdf:about=""http://slashdot.org/article.pl?sid=04/11/25/154216&amp;from=rss"">
					<title>" + m_ItemTitles[2] + @"</title>
					<link>" + m_ItemLinks[2] + @"</link>
					<description>" + m_ItemSummaries[2] + @"</description>
					<dc:creator>michael</dc:creator>
					<dc:subject>pilot</dc:subject>
					<dc:date>2004-11-25T17:38:00+00:00</dc:date>
					<slash:section>mainpage</slash:section>
					<slash:department>oops</slash:department>
					<slash:comments>41</slash:comments>
					<slash:hitparade>41,37,27,17,4,2,2</slash:hitparade>
					</item>

					<textinput rdf:about=""http://slashdot.org/search.pl"">
					<title>Search Slashdot</title>
					<description>Search Slashdot stories</description>
					<name>query</name>
					<link>http://slashdot.org/search.pl</link>
					</textinput>

					</rdf:RDF>" );
				
				// save document to default location for feed
				rssDoc.SaveToFile( m_TempFile, "RSS10ToXmlTest" );
				FeedToXmlTest( MIN_ITEM_COUNT ); // convert and run tests
			}

			[Test] public void RSS092ToXmlTest()
			{
				// load sample rss 0.92 document for test
				SafeXmlDoc rssDoc = new SafeXmlDoc( @"<?xml version=""1.0""?>
					<rss version=""0.92"">
					<channel>
					<title>" + FEED_NAME + @"</title>
					<link>" + FEED_LINK + @"</link>
					<description>It's not news, it's fark</description>
					<image>
					<title>Fark RSS</title>
					<url>http://img.fark.com/images/2002/links/fark.gif</url>
					<link>http://www.fark.com/</link>
					</image>
					<lastBuildDate></lastBuildDate>
					
					<item>
					<title>" + m_ItemTitles[0] + @"</title>
					<link>" + m_ItemLinks[0] + @"</link>
					<description>" + m_ItemSummaries[0] + @"</description>
					</item>

					<item>
					<title>" + m_ItemTitles[1] + @"</title>
					<link>" + m_ItemLinks[1] + @"</link>
					<description>" + m_ItemSummaries[1] + @"</description>
					</item>

					<item>
					<title>" + m_ItemTitles[2] + @"</title>
					<link>" + m_ItemLinks[2] + @"</link>
					<description>" + m_ItemSummaries[2] + @"</description>
					</item>

					</channel></rss>" );
				
				// save document to default location for feed
				rssDoc.SaveToFile( m_TempFile, "RSS092ToXmlTest" );
				FeedToXmlTest( MIN_ITEM_COUNT ); // convert feed and run tests
			}

			[Test] public void RSS090ToXmlTest()
			{
				// load sample rss 0.90 document for test
				SafeXmlDoc rssDoc = new SafeXmlDoc( @"<?xml version=""1.0""?>
					<rdf:RDF
					xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
					xmlns=""http://my.netscape.com/rdf/simple/0.9/"">

					<channel>
						<title>" + FEED_NAME + @"</title>
						<link>" + FEED_LINK + @"</link>
						<description>the Mozilla Organization web site</description>
					</channel>
					<image>
						<title>Mozilla</title>
						<url>http://www.mozilla.org/images/moz.gif</url>
						<link>http://www.mozilla.org</link>
					</image>
					<item>
						<title>" + m_ItemTitles[0] + @"</title>
						<link>" + m_ItemLinks[0] + @"</link>
					</item>
					<item>
						<title>" + m_ItemTitles[1] + @"</title>
						<link>" + m_ItemLinks[1] + @"</link>
					</item>
					<item>
						<title>" + m_ItemTitles[2] + @"</title>
						<link>" + m_ItemLinks[2] + @"</link>
					</item>
					</rdf:RDF>" );
				
				// save document to default location for feed
				rssDoc.SaveToFile( m_TempFile, "RSS090ToXmlTest" );
				FeedToXmlTest( MIN_ITEM_COUNT ); // convert and run tests
			}

			private void FeedToXmlTest( int itemCount )
			{
				// load feed from saved location on disk
				FeedModel feed = new FeedModel( m_Guid, "", m_TempFolder );

				string feedXml = feed.ToXml();
				SafeXmlDoc feedDoc = new SafeXmlDoc( feedXml );
	
				string feedName = feedDoc.GetInnerText( "/channel/title", "ToXmlTest" );
				Assert.AreEqual( feed.CleanName( FEED_NAME ), feedName, "Got incorrect feed name from xml doc" );

				string feedLink = feedDoc.GetInnerText( "/channel/link", "ToXmlTest" );
				Assert.AreEqual( FEED_LINK, feedLink, "Got incorrect feed link from xml doc" );
	
				XmlNodeList itemNodes = feedDoc.GetNodes( "/channel/item", "ToXmlTest" );
				Assert.AreEqual( itemCount, itemNodes.Count, "Got incorrect item count from doc" );
				
				for( int i = 0; i < MIN_ITEM_COUNT; i++ ) // test for minimum item count
				{
					XmlNode itemNode = itemNodes[i]; // get item node at the current index
					string expectedTitle = m_ItemTitles[i]; // parse title to match InnerText
					if( expectedTitle.IndexOf( "CDATA" ) == -1 )
					{
						expectedTitle = Regex.Replace( expectedTitle, @"</*[pbr]+[""=\s\w]*/*>", "" );
						expectedTitle = HttpUtility.HtmlDecode( expectedTitle );
					}
					else expectedTitle = expectedTitle.Replace( "<![CDATA[", "" ).Replace( "]]>", "" );
					
					Assert.AreEqual( expectedTitle, itemNode.SelectSingleNode( "title" ).InnerText, "Got incorrect title at index: " + i.ToString() );
					Assert.AreEqual( HttpUtility.HtmlDecode( m_ItemLinks[i] ), itemNode.SelectSingleNode( "link" ).InnerText, "Got incorrect link at index: " + i.ToString() );
				}
			}
		}

		#endif
		#endregion
	}
}
