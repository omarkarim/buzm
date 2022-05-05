using System.Collections.Specialized;

namespace Buzm.Hives
{
	/// <summary>Front controller interface for all hive management
	/// events. Each discrete UI type should implement separately</summary>
	public interface IHiveController
	{
		void NewPost( string hiveGuid, NameValueCollection info );
		void NewFeed( string hiveGuid, NameValueCollection info );
		
		void EditPost( string hiveGuid, string postGuid, NameValueCollection info );
		void EditFeed( string hiveGuid, string feedGuid, NameValueCollection info );

		void RemovePost( string hiveGuid, string postGuid );
		void RemoveFeed( string hiveGuid, string feedGuid );
	}
}
