using System;
using Buzm.Network.Web;

namespace Buzm.Hives
{
	public static class RestEventRouter
	{
		/// <summary>Maps REST event data to  hive controller method calls </summary>
		public static void ProcessRestEvent( IHiveController controller, RestEventArgs args )
		{
			if( ( controller != null ) && ( args != null ) )
			{
				string hiveGuid = args.GetFirstParamValue( "hives" );
				if( !String.IsNullOrEmpty( hiveGuid ) )
				{
					string postGuid = args.GetFirstParamValue( "posts" );
					string feedGuid = args.GetFirstParamValue( "feeds" );

					switch( args.Method ) // map HTTP method to function
					{
						case HttpMethods.POST: // create new item

							if( postGuid != null )
								controller.NewPost( hiveGuid, args.Params );
							else if( feedGuid != null )
								controller.NewFeed( hiveGuid, args.Params );
							break;

						case HttpMethods.PUT: // update an existing item
							
							if( !String.IsNullOrEmpty( postGuid ) )
								controller.EditPost( hiveGuid, postGuid, args.Params );
							else if( !String.IsNullOrEmpty( feedGuid ) )
								controller.EditFeed( hiveGuid, feedGuid, args.Params );
							break;
				
						case HttpMethods.DELETE: // remove existing item

							if( !String.IsNullOrEmpty( postGuid ) )
								controller.RemovePost( hiveGuid, postGuid );
							else if( !String.IsNullOrEmpty( feedGuid ) )
								controller.RemoveFeed( hiveGuid, feedGuid );
							break;
					}
				}
			}			
		}
	}
}
