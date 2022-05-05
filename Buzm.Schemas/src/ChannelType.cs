using System;
using System.Xml;
using System.Xml.Serialization;
using System.Collections;
using NUnit.Framework;
using Buzm.Utility;

namespace Buzm.Schemas
{
	[XmlRoot( "channel", Namespace="", IsNullable=false )]
	public class ChannelType : ContentType
	{				
		private ArrayList m_Items;
		private ArrayList m_Channels;

		public ChannelType()
		{
			m_Items	= new ArrayList( );
			m_Channels = new ArrayList( );			
		}

		[XmlElement("item", typeof(ItemType))] public ArrayList Items
		{ get { return m_Items; } set { m_Items = value; } }

		[XmlElement("channel", typeof(ChannelType))] public ArrayList Channels
		{ get { return m_Channels; } set { m_Channels = value; } }
				
		#region NUnit Automated Test Cases

		[TestFixture] public class ChannelTypeTest
		{
			[SetUp] public void SetUp(){ }
			[TearDown] public void TearDown() { }
			
			[Test] public void SerializeTest()
			{ 							
				// setup a reference date for posts
				DateTime customPostTime = DateTime.Now;
				
				// setup a channel to serialize
				ChannelType channel = new ChannelType();
				channel.Guid = "\x0001channel <p> guid";
				channel.Title = "\x000bchannel title <p>";
				channel.Link = "http://www.yahoo.com";
				channel.Tags = "channel tags \"quoted\"";
				channel.Summary = "Påskön ar ön på jardklatet";
				channel.Modified = customPostTime;
				channel.Posted = customPostTime;
				channel.Position = "1,1";
				channel.Priority = "50";

				ItemType itemOne = new ItemType();
				itemOne.Guid = "item guid 1";	
				itemOne.Posted = customPostTime;
				itemOne.Title = "<![CDATA[item title 1]]>";
				
				AuthorType authorOne = new AuthorType();
				authorOne.Login = "okarim";
				itemOne.Author = authorOne;

				ItemType itemTwo = new ItemType();
				itemTwo.Guid = "item guid 2]]>]]>";
				itemTwo.Title = "item title 2]]>]]>";
				itemTwo.Tags = "item tags 2";

				channel.Items.Add( itemOne );
				channel.Items.Add( itemTwo );

				ChannelType subchannelOne = new ChannelType();
				subchannelOne.Guid = "subchannel one guid";
				subchannelOne.Title = "subchannel one title";
				subchannelOne.Tags = "subchannel one tags";
				subchannelOne.Position = "1,1";
				
				ItemType subitemOne = new ItemType();
				subitemOne.Guid = "subitem guid 1";
				subitemOne.Title = "subitem title 1";
				subitemOne.Tags = "subitem tags 1";
				
				ItemType subitemTwo = new ItemType();
				subitemTwo.Title = ""; // test empty cdata
				subitemTwo.Priority = "75"; // set non-default
				
				subchannelOne.Items.Add( subitemOne );
				subchannelOne.Items.Add( subitemTwo );

				ChannelType subchannelTwo = new ChannelType();
				subchannelTwo.Guid = "subchannel two guid";
				subchannelTwo.Title = "subchannel two title";
				subchannelTwo.Position = "1,1";
				subchannelTwo.Priority = "75";

				channel.Channels.Add( subchannelOne );
				channel.Channels.Add( subchannelTwo );

				// serialize channel to string
				string channelXml = channel.ToXml();
				SafeXmlDoc channelXmlDoc = new SafeXmlDoc( channelXml );
				
				// Assertion.Assert( "Got incorrect xml: " + channelXml, false );
				XmlNode cdataTitleNode = channelXmlDoc.SelectSingleNode( "/channel/title" );
				Assert.AreEqual( "<![CDATA[channel title <p>]]>", cdataTitleNode.InnerXml, "Got incorrect channel title cdata" );
				
				Assert.AreEqual( "channel <p> guid", channelXmlDoc.GetInnerText( "/channel/guid", "" ), "Got incorrect channel guid" );
				Assert.AreEqual( "channel title <p>", channelXmlDoc.GetInnerText( "/channel/title", "" ), "Got incorrect channel title" );

				Assert.AreEqual( "channel tags \"quoted\"", channelXmlDoc.GetInnerText( "/channel/tags", "" ), "Got incorrect channel tags" );
				Assert.AreEqual( "Påskön ar ön på jardklatet", channelXmlDoc.GetInnerText( "/channel/summary", "" ), "Got incorrect channel summary" );				
				Assert.AreEqual( Format.DateToString( customPostTime ), channelXmlDoc.GetInnerText( "/channel/posted", "" ), "Got incorrect channel post time" );
				
				Assert.AreEqual( "item title 1", channelXmlDoc.GetInnerText( "/channel/item[1]/title", "" ), "Got incorrect item 1 title" );
				Assert.AreEqual( "okarim", channelXmlDoc.GetInnerText( "/channel/item[1]/author/login", "" ), "Got incorrect item 1 author" );
				Assert.AreEqual( Format.DateToString( customPostTime ), channelXmlDoc.GetInnerText( "/channel/item[1]/posted", "" ), "Got incorrect item 1 post time" );

				Assert.AreEqual( "item title 2", channelXmlDoc.GetInnerText( "/channel/item[2]/title", "" ), "Got incorrect item 2 title" );
				Assert.AreEqual( "item tags 2", channelXmlDoc.GetInnerText( "/channel/item[2]/tags", "" ), "Got incorrect item 2 tags" );

				Assert.AreEqual( "subchannel one guid", channelXmlDoc.GetInnerText( "/channel/channel[1]/guid", "" ), "Got incorrect subchannel guid" );
				Assert.AreEqual( "subchannel one tags", channelXmlDoc.GetInnerText( "/channel/channel[1]/tags", "" ), "Got incorrect subchannel tags" );
				
				Assert.AreEqual( "subitem title 1", channelXmlDoc.GetInnerText( "/channel/channel[1]/item[1]/title", "" ), "Got incorrect subchannel, subitem title" );
				Assert.AreEqual( "subitem tags 1", channelXmlDoc.GetInnerText( "/channel/channel[1]/item[1]/tags", "" ), "Got incorrect subchannel, subitem tags" );

				Assert.AreEqual( subitemTwo.Guid, channelXmlDoc.GetInnerText( "/channel/channel[1]/item[2]/guid", "" ), "Got incorrect subitem two guid" );
				Assert.AreEqual( String.Empty, channelXmlDoc.GetInnerText( "/channel/channel[1]/item[2]/title", "" ), "Got incorrect subitem two title" );
				Assert.AreEqual( "75", channelXmlDoc.GetInnerText( "/channel/channel[1]/item[2]/priority", "" ), "Got incorrect subitem two priority" );
			}
		}

		#endregion
	}
}
