using System;

namespace Buzm.Network.Packets
{
	/// <summary>Used to transport data for 
	/// unique feed instance in a Hive</summary>
	[Serializable] public class FeedPacket : Packet
	{
		private string m_FeedGuid; // uniquely identifies feed instance
		
		public FeedPacket( string text, string hiveGuid, string feedGuid ) 
		: base( text, hiveGuid ) // initialize base data packet
		{
			m_FeedGuid = feedGuid; // initialize feed guid
		}

		public string FeedGuid
		{ 
			get { return m_FeedGuid; } 
			set { m_FeedGuid = value; }
		}
	}
}