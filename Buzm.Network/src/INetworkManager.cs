using System;
using Buzm.Network.Packets;

namespace Buzm.Network
{
	public interface INetworkManager
	{
		Packet GetNextPacket();
		void SetNextPacket( Packet pkt );
		bool NotifyUser { get; set; }
		void Close();		
	}
}