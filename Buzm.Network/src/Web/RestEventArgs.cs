using System;
using System.Web;
using System.Collections.Specialized;
using NUnit.Framework;

namespace Buzm.Network.Web
{
	// delegate used to pass information about a REST event
	public delegate void RestEventHandler( object sender, RestEventArgs e );

	/// <summary>Encapsulates a generic Representational State Transfer (REST) event. 
	/// The event may be raised locally, over a P2P network, or via HTTP</summary>
	public class RestEventArgs
	{
		private Uri m_Uri;		
		private string m_Data;

		private HttpMethods m_Method;
		private NameValueCollection m_Params;

		// path and guid characters that need to be trimmed
		private char[] TRIM_CHARS = new char[] { '/', '{', '}' };
		
		public RestEventArgs( string method, string uri, string data )
		{
			m_Data = data; // used for POST and PUT payload
			m_Method = (HttpMethods)Enum.Parse( typeof(HttpMethods), method, true );			
			
			m_Uri = new Uri( uri, UriKind.Absolute );
			m_Params = ParseUriParams( m_Uri );
		}

		public NameValueCollection ParseUriParams( Uri uri )
		{
			string paramName = null; // loop pair variable
			NameValueCollection parameters = new NameValueCollection();
			
			// extract Rest param pairs in uri path
			foreach( string segment in uri.Segments )
			{
				string clean = HttpUtility.UrlDecode( segment );
				string param = clean.Trim( TRIM_CHARS ); // trim path
				
				if( Enum.IsDefined( typeof(RestParams), param.ToLower() ) )
				{
					paramName = param;
					parameters[param] = String.Empty;
				}
				else if( paramName != null )
				{					
					parameters[paramName] = param;
					paramName = null; 
				}
			}
			
			parameters.Add( HttpUtility.ParseQueryString( uri.Query ) );
			return parameters; // supplemented with query params
		}

		public string GetFirstParamValue( string paramName )
		{
			string[] values = m_Params.GetValues( paramName );
			
			if( ( values != null ) && ( values.Length > 0 ) ) 
				return values[0]; // return first value
			else return null; // null or does not exist
		}

		public NameValueCollection Params
		{
			get { return m_Params; }
			set { m_Params = value; }
		}

		public Uri Uri
		{
			get { return m_Uri; }
			set { m_Uri = value; }
		}		

		public string Data
		{
			get { return m_Data; }
			set { m_Data = value; }
		}

		public HttpMethods Method
		{
			get { return m_Method; }
			set { m_Method = value; }
		}

		public bool IsLocal
		{
			get { return m_Uri.IsLoopback; }
		}

		public enum RestParams : int
		{
			users, hives, feeds, posts	
		}

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class RestEventArgsTest
		{
			[SetUp] public void SetUp() { }
			[TearDown] public void TearDown() { }

			[Test] public void ParseTest()
			{
				string fileUri = @"file://C:\Program Files\Buzm\Data\Users\omar\Hives\%7B7aa4e84b-a1fc-4b55-baf7-ff7b4d0e8dcd%7D\posts\1";
				RestEventArgs args = new RestEventArgs( "post", fileUri, "postData" );

				Assert.IsTrue( args.IsLocal, "Got incorrect local/loopback value from file uri" );
				Assert.AreEqual( HttpMethods.POST, args.Method, "Got incorrect method for file event" );

				Assert.AreEqual( 3, args.Params.Count, "Got incorrect param count from file uri" );
				Assert.AreEqual( "omar", args.Params["users"], "Got incorrect user value from file uri" );

				Assert.AreEqual( "7aa4e84b-a1fc-4b55-baf7-ff7b4d0e8dcd", args.Params["hives"], "Got incorrect hive value from file uri" );
				Assert.AreEqual( "1", args.Params["Posts"], "Got incorrect post value from file uri" );

				string localHttpUri = @"http://localhost/USERS/hives/{1}/x/Feeds/0/default.htm?rnd=123";
				args = new RestEventArgs( "GET", localHttpUri, null );

				Assert.IsTrue( args.IsLocal, "Got incorrect loopback value from local http uri" );
				Assert.AreEqual( HttpMethods.GET, args.Method, "Got incorrect method for local http event" );

				Assert.AreEqual( 4, args.Params.Count, "Got incorrect param count from local http uri" );
				Assert.AreEqual( String.Empty, args.Params["users"], "Got incorrect user value from local http uri" );

				Assert.AreEqual( "1", args.Params["Hives"], "Got incorrect hive value from local http uri" );
				Assert.AreEqual( "0", args.Params["Feeds"], "Got incorrect feed value from local http uri" );

				Assert.AreEqual( "123", args.Params["RND"], "Got incorrect query value from local http uri" );
				Assert.IsNull( args.Params["Posts"], "Got unexpected posts value from local http uri" );

				string remoteHttpUri = @"http://buzm.com/hives/hives/%7Bguid1%7D/Feeds?rnd=%20123&Hives=guid2";
				args = new RestEventArgs( "Put", remoteHttpUri, String.Empty );

				Assert.IsFalse( args.IsLocal, "Got incorrect loopback value from remote http uri" );
				Assert.AreEqual( HttpMethods.PUT, args.Method, "Got incorrect method for remote http event" );

				Assert.AreEqual( 3, args.Params.Count, "Got incorrect param count from remote http uri" );
				Assert.AreEqual( "guid1,guid2", args.Params["HIVES"], "Got incorrect hive values from remote http uri" );
				
				Assert.AreEqual( String.Empty, args.Params["Feeds"], "Got incorrect feed value from remote http uri" );
				Assert.AreEqual( " 123", args.Params["Rnd"], "Got incorrect query value from remote http uri" );				
			}

			[ExpectedException( "System.UriFormatException" )]
			[Test] public void ParseBadUriTest()
			{
				// create event args with invalid uri argument
				RestEventArgs args = new RestEventArgs( "get", "/relative", "" );				
			}

			[ExpectedException( "System.UriFormatException" )]
			[Test] public void ParseEmptyUriTest()
			{
				// create event args with empty uri argument
				RestEventArgs args = new RestEventArgs( "get", "", "" );
			}

			[ExpectedException( "System.ArgumentNullException" )]
			[Test] public void ParseNullUriTest()
			{
				// create event args with null uri argument
				RestEventArgs args = new RestEventArgs( "get", null, "" );
			}

			[ExpectedException( "System.ArgumentException" )]
			[Test] public void ParseBadMethodTest()
			{
				// create event args with invalid method argument 
				RestEventArgs args = new RestEventArgs( "vet", "", "" );				
			}

			[ExpectedException( "System.ArgumentNullException" )]
			[Test] public void ParseNullMethodTest()
			{
				// create event args with null method argument 
				RestEventArgs args = new RestEventArgs( null, "", "" );
			}
		}

		#endif
		#endregion
	}
}
