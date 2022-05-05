using System;
using System.IO;
using System.Xml;
using Buzm.Utility;
using NUnit.Framework;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Buzm.Register
{
	/// <summary>Represents a Buzm user</summary>
	public class User
	{
		// static thread-safe regexes
		private static Regex m_LoginRegex;
		private static Regex m_EmailRegex;
		private static Regex m_PasswordRegex;

		// user data storage vars
		private string m_UserFilePath;
		private SafeXmlDoc m_UserXmlDoc;

		private bool m_RememberLogin;
		private UserLoginState m_LoginState;

		static User( )
		{
			// logins allow letters & numbers separated by dashes & underscores
			// only since they must be valid filenames as well as email prefixes
			// periods are illegal in logins as they're used for file extensions
			m_LoginRegex	= new Regex( @"^([0-9a-zA-Z]([-\w]*[0-9a-zA-Z])*)$" ); 
			m_EmailRegex	= new Regex( @"^([0-9a-zA-Z]([-.\w]*[0-9a-zA-Z])*@(([0-9a-zA-Z])+"
									   + @"([-\w]*[0-9a-zA-Z])*\.)+[a-zA-Z]{2,9})$" );
			m_PasswordRegex	= new Regex( @"^(?=.*\d).{4,15}$" ); // require one digit
		}

		public User( )
		{
			m_UserXmlDoc = new SafeXmlDoc( ); // exception-safe wrapper
			string userXml = Config.GetOuterXml( "templates/config/user" );
			m_UserXmlDoc.LoadFromString( userXml, "User.User" );
		}

		public User( string file )
		{
			m_UserFilePath = file;
			m_UserXmlDoc = new SafeXmlDoc( ); 
			m_UserXmlDoc.LoadFromFile( file, "User.User" );
		}
		
		public User( SafeXmlDoc safeXmlDoc )
		{	
			// load existing user xml
			m_UserXmlDoc = safeXmlDoc; 
		}

		/// <summary>Makes a copy of the
		/// user with the identification
		/// credentials populated</summary>
		/// <returns>The cloned user</returns>
		public User CloneIdentity( )
		{
			User clone = new User( );
			clone.Login = this.Login;
			clone.Password = this.Password;
			return clone; // with same identity
		}

		public bool SaveToDisk( )
		{
			// save xml document to disk at the location is was loaded from
			if( m_UserFilePath != null ) return SaveToDisk( m_UserFilePath );
			else return false; // xml document was not loaded from known file
		}

		public bool SaveToDisk( string file )
		{
			// save xml document to disk at the specified location
			return m_UserXmlDoc.SaveToFile( file, "User.SaveXmlFile" );
		}

		public void SaveToConfig()
		{
			// serialize user xml to config setting
			Config.Settings.AutoLogin = this.ToXmlString();
		}

		public static User LoadFromConfig()
		{
			string userXml = Config.Settings.AutoLogin;
			if( !String.IsNullOrEmpty( userXml ) )
			{
				SafeXmlDoc userXmlDoc = new SafeXmlDoc( userXml );
				User user = new User( userXmlDoc ); // new user from doc

				string login = user.Login; // validate restored user login
				if( !String.IsNullOrEmpty( login ) && CheckLoginFormat( login ) )
				{
					user.RememberLogin = true;
					return user; // from config
				}
			}
			return null; // no config user
		}

		public string Guid
		{
			get { return m_UserXmlDoc.GetInnerText( "/user/guid", "User" ); }
			set { m_UserXmlDoc.SetInnerText( "/user/guid", value, "User" ); }
		}

		public string Login
		{
			get { return m_UserXmlDoc.GetInnerText( "/user/login", "User" ); }
			set { m_UserXmlDoc.SetInnerText( "/user/login", value, "User" ); }
		}

		public string Email
		{
			get { return m_UserXmlDoc.GetInnerText( "/user/email", "User" ); }
			set { m_UserXmlDoc.SetInnerText( "/user/email", value, "User" ); }
		}

		public string Password
		{
			get { return m_UserXmlDoc.GetInnerText( "/user/password", "User" ); }
			set { m_UserXmlDoc.SetInnerText( "/user/password", value, "User" ); }
		}

		public XmlNodeList Hives
		{
			// clients cannot set Hives as a group - must add them one a time
			get { return m_UserXmlDoc.GetNodes( "/user/hives/hive", "User" ); }			
		}

		
		public XmlNode GetHive()
		{
			// get the first hive in hives collection. Null if none exists
			return m_UserXmlDoc.GetNode( "/user/hives/hive", "User.GetHive" );
		}
				
		public XmlNode GetHive( string hiveGuid )
		{
			// get a hive given its guid in hives collection. Null if no hive match
			return m_UserXmlDoc.GetUniqueChild( "/user/hives", hiveGuid, "User.GetHive" );
		}
				
		public XmlNode SetHive( string hiveXml )
		{
			// add or update hive depending on its guid within the hives collection
			return m_UserXmlDoc.SetUniqueChild( "/user/hives", hiveXml, "User.SetHive" );
		}

		public XmlNode RemoveHive( string hiveGuid )
		{
			// remove hive from the hives collection based on its guid child element
			return m_UserXmlDoc.RemoveUniqueChild( "/user/hives", hiveGuid, "User.RemoveHive" );
		}

		/// <summary>Clones hive settings from the specified user. Hive and feed guids 
		/// are reset so the copied items are independent of the originals. Note that 
		/// existing hives with the same guid will be erased in the process</summary>
		public void CloneHives( User fromUser )
		{
			try // to clone hive settings from user
			{
				XmlNodeList originHiveNodes = fromUser.Hives;
				if( originHiveNodes != null ) // got origin hives
				{
					// iterate origin hives and copy to user profile
					foreach( XmlNode originHiveNode in originHiveNodes )
					{
						XmlNode cloneHiveNode = SetHive( originHiveNode.OuterXml );
						if( cloneHiveNode != null ) // hive was set normally
						{
							XmlNode hostNode = cloneHiveNode.SelectSingleNode( "host" );
							if( hostNode != null ) hostNode.InnerText = String.Empty;

							XmlNode membersNode = cloneHiveNode.SelectSingleNode( "members" );
							if( membersNode != null ) membersNode.RemoveAll(); // clear members

							XmlNode createDateNode = cloneHiveNode.SelectSingleNode( "createDate" );
							if( createDateNode != null ) // change created date to current time
								createDateNode.InnerText = Format.DateToString( DateTime.Now );							

							XmlNode guidNode = cloneHiveNode.SelectSingleNode( "guid" );
							if( guidNode != null ) // guid node exists in config
							{
								string hiveGuid = System.Guid.NewGuid().ToString();
								guidNode.InnerText = hiveGuid; // change guid

								XmlNodeList feedNodes = GetFeeds( hiveGuid );
								if( feedNodes != null ) // got clone feeds
								{
									// iterate hive feeds and update guids
									foreach( XmlNode feedNode in feedNodes )
									{
										XmlNode feedGuidNode = feedNode.SelectSingleNode( "guid" );
										if( feedGuidNode != null ) // guid node exists
										{
											string feedGuid = System.Guid.NewGuid().ToString();
											feedGuidNode.InnerText = feedGuid;
										}
									}
								}
							}													
						}
					}
				}
			}
			catch( Exception e )
			{
				Log.Write( "Failed to clone hives between users",
				TraceLevel.Warning, "User.CloneHives", e );
			}
		}		
					
		public XmlNodeList GetFeeds( string hiveGuid )
		{
			string feedsQuery = "/user/hives/hive[guid='" + hiveGuid + "']/feeds/feed";
			return m_UserXmlDoc.GetNodes( feedsQuery, "User.GetFeeds" ); // return feeds	
		}
		
		public XmlNode GetFeed( string hiveGuid, string feedGuid )
		{
			string feedsQuery = "/user/hives/hive[guid='" + hiveGuid + "']/feeds";
			return m_UserXmlDoc.GetUniqueChild( feedsQuery, feedGuid, "User.GetFeed" );
		}

		public XmlNode SetFeed( string hiveGuid, string feedXml )
		{
			string feedsQuery = "/user/hives/hive[guid='" + hiveGuid + "']/feeds";
			return m_UserXmlDoc.SetUniqueChild( feedsQuery, feedXml, "User.SetFeed" );
		}

		public XmlNode RemoveFeed( string hiveGuid, string feedGuid )
		{
			string feedsQuery = "/user/hives/hive[guid='" + hiveGuid + "']/feeds";
			return m_UserXmlDoc.RemoveUniqueChild( feedsQuery, feedGuid, "User.RemoveFeed" );
		}
		
		public XmlNodeList GetMembers( string hiveGuid )
		{
			string membersQuery = "/user/hives/hive[guid='" + hiveGuid + "']/members/user";
			return m_UserXmlDoc.GetNodes( membersQuery, "User.GetMembers" ); // return members	
		}
		
		public XmlNode GetMember( string hiveGuid, string memberGuid )
		{
			string membersQuery = "/user/hives/hive[guid='" + hiveGuid + "']/members";
			return m_UserXmlDoc.GetUniqueChild( membersQuery, memberGuid, "User.GetMember" );
		}
		
		public XmlNode SetMember( string hiveGuid, string memberXml )
		{
			string membersQuery = "/user/hives/hive[guid='" + hiveGuid + "']/members";
			return m_UserXmlDoc.SetUniqueChild( membersQuery, memberXml, "User.SetMember" );
		}

		public XmlNode RemoveMember( string hiveGuid, string memberGuid )
		{
			string membersQuery = "/user/hives/hive[guid='" + hiveGuid + "']/members";
			return m_UserXmlDoc.RemoveUniqueChild( membersQuery, memberGuid, "User.RemoveMember" );
		}

		public XmlNode RemoveMemberByLogin( string hiveGuid, string memberLogin )
		{
			try  // removing unique member based on login element value
			{ 
				string membersQuery = "/user/hives/hive[guid='" + hiveGuid + "']/members";
				XmlNode members = m_UserXmlDoc.SelectSingleNode( membersQuery );
				if( members != null ) // members node should always exist
				{
					XmlNode remMember = members.SelectSingleNode( "*[login='" + memberLogin + "']" );
					if( remMember != null ) return members.RemoveChild( remMember );
				}
			}
			catch( Exception e )
			{
				Log.Write( "Error while removing the member from user xml",
				TraceLevel.Warning, "User.RemoveMemberByLogin", e );				
			}
			return null; // since remove child was not reached
		}

		public XmlNode GetMessage( string messageGuid )
		{
			// get a message given its guid in messages collection. Null if no match
			return m_UserXmlDoc.GetUniqueChild( "/user/messages", messageGuid, "User.GetMessage" );
		}

		public XmlNode SetMessage( string messageXml )
		{
			// add or update message depending on its guid in the messages collection
			return m_UserXmlDoc.SetUniqueChild( "/user/messages", messageXml, "User.SetMessage" );
		}

		public string DataFolder
		{
			// TODO: allow users to specify a custom data folder on their machine
			get { return Config.GetFolderValue( "preferences/users/folder" ) + Login; }
		}

		public string ToXmlString( )
		{
			// return outerxml in a string
			return m_UserXmlDoc.ToString( );
		}		

		/// <summary> Checks formats of all profile fields 
		/// that might be malformed by user input </summary>
		public bool CheckProfileFormats( out string resultMsg )
		{
			if( !CheckLoginFormat( Login ) ) 
			{	
				// TODO: Move hardcoded result messages into a resource file
				resultMsg = "The Buzm ID you chose is invalid - Please use letters and numbers with dashes or underscores between them.";
				return false;
			}
			if( !CheckEmailFormat( Email ) ) 
			{
				resultMsg = "The email address you entered appears invalid - Please try a different one.";
				return false;
			}
			if( !CheckPasswordFormat( Password ) ) 
			{
				resultMsg = "Buzm passwords must be between 4 and 15 characters and contain at least one number.";
				return false;
			}
			resultMsg = "";
			return true;
		}

		/// <summary> Checks the input string against the
		/// static regex of legal login formats. Logins should 
		/// be valid email prefixes and safe filenames </summary>
		public static bool CheckLoginFormat( string login )
		{
			return m_LoginRegex.IsMatch( login );
		}

		/// <summary> Checks the input string against the
		/// static regex of legal email formats </summary>
		public static bool CheckEmailFormat( string email )
		{
			return m_EmailRegex.IsMatch( email );
		}

		/// <summary> Checks the input string against the
		/// static regex of legal password formats </summary>
		public static bool CheckPasswordFormat( string password )
		{
			return m_PasswordRegex.IsMatch( password );
		}

		/// <summary> Checks that the specified user is 
		/// not null and has been authenticated</summary>
		public static bool IsAlive( User user )
		{
			if( ( user != null ) && ( user.LoginState > UserLoginState.None ) )
				return true; // user logged in
			else return false;
		}

		public UserLoginState LoginState
		{
			get { return m_LoginState; }
			set { m_LoginState = value; }
		}

		public bool RememberLogin
		{
			get { return m_RememberLogin; }
			set { m_RememberLogin = value; }
		}

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class UserTest
		{
			[SetUp] public void SetUp() 
			{ 
				// Load local config file for the Buzm.Utility assembly
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly );
			}

			[TearDown] public void TearDown() { }
			
			[Test] public void LoginFormatTest()
			{
				// Logins should only contain letters and digits with dashes and underscores between them
				string[] illegalChars = new string[]{ ".", "?", "\\", ":", ";", "*", "<", ">", "{", "}", "`", "!",
														"|", "~", "^", "%", "&", "(", ")", "+", "=", "'", "@", ",",
														"#", "$", "[", "]", " ", "/", "\"", "\n", "\t", "\r" };

				// test logins with various illegal chars 
				for( int i=0; i < illegalChars.Length; i++ )
				{
					Assertion.Assert( @"Illegal login contains: " + illegalChars[i], 
									  !User.CheckLoginFormat( "o" + illegalChars[i] + "k" ) );
				}

				// test logins with dashes and underscores at the start and end
				Assertion.Assert( "Invalid login format: omarkarim_", !User.CheckLoginFormat( "omarkarim_" ) );
				Assertion.Assert( "Invalid login format: _omarkarim", !User.CheckLoginFormat( "_omarkarim" ) );
				Assertion.Assert( "Invalid login format: omarkarim-", !User.CheckLoginFormat( "omarkarim-" ) );
				Assertion.Assert( "Invalid login format: -omarkarim", !User.CheckLoginFormat( "-omarkarim" ) );
				Assertion.Assert( "Invalid login format: Empty String", !User.CheckLoginFormat( "" ) );
				
				// test some legal logins as well
				Assertion.Assert( "Login format should be valid: o", User.CheckLoginFormat( "o" ) );
				Assertion.Assert( "Login format should be valid: okarim01", User.CheckLoginFormat( "okarim01" ) );
				Assertion.Assert( "Login format should be valid: omar_karim", User.CheckLoginFormat( "omar_karim" ) );
				Assertion.Assert( "Login format should be valid: omar-karim", User.CheckLoginFormat( "omar-karim" ) );
				Assertion.Assert( "Login format should be valid: 1-omar_1k2", User.CheckLoginFormat( "1-omar_1k2" ) );
			}

			[Test] public void EmailFormatTest()
			{
				// Test a variety of illegal email formats 
				Assertion.Assert( "Invalid email format: omar@com", !User.CheckEmailFormat( "omar@com" ) );
				Assertion.Assert( "Invalid email format: omar@.com", !User.CheckEmailFormat( "omar@.com" ) );
				Assertion.Assert( "Invalid email format: omar@com.", !User.CheckEmailFormat( "omar@com." ) );
				Assertion.Assert( "Invalid email format: omar@.x.com", !User.CheckEmailFormat( "omar@.x.com" ) );
				Assertion.Assert( "Invalid email format: omar@x.com.", !User.CheckEmailFormat( "omar@x.com." ) );
				Assertion.Assert( "Invalid email format: @yahoo.com", !User.CheckEmailFormat( "@buzm.com" ) );
				Assertion.Assert( "Invalid email format: a_@yahoo.com", !User.CheckEmailFormat( "a_@buzm.com" ) );
				Assertion.Assert( "Invalid email format: -a@yahoo.com", !User.CheckEmailFormat( "-a@buzm.com" ) );
				Assertion.Assert( "Invalid email format: a@yahoo.com", !User.CheckEmailFormat( "a@buzm.comnebuzms" ) );
			
				// Test some legal email formats as well
				Assertion.Assert( "Email format should be valid: x@x.x.com", User.CheckEmailFormat( "x@x.x.com" ) );
				Assertion.Assert( "Email format should be valid: omar_karim@buzm.com", User.CheckEmailFormat( "omar_karim@buzm.com" ) );
				Assertion.Assert( "Email format should be valid: omar-karim@buzm.com", User.CheckEmailFormat( "omar-karim@buzm.com" ) );
				Assertion.Assert( "Email format should be valid: omar-karim@a.buzm.com", User.CheckEmailFormat( "omar-karim@a.buzm.com" ) );
			}

			[Test] public void PasswordFormatTest()
			{
				// Test a variety of illegal password formats 
				Assertion.Assert( "Invalid password format: b0z", !User.CheckPasswordFormat( "b0z" ) );
				Assertion.Assert( "Invalid password format: buzm", !User.CheckPasswordFormat( "buzm" ) );
				Assertion.Assert( "Invalid password format: buzmer", !User.CheckPasswordFormat( "buzmer" ) );
				Assertion.Assert( "Invalid password format: Empty String", !User.CheckPasswordFormat( "" ) );
				Assertion.Assert( "Invalid password format: buzmbuzmbuzmbuzm", !User.CheckPasswordFormat( "buzmbuzmbuzmbuzm" ) );
				
				// Test some legal password formats as well
				Assertion.Assert( "Password format should be valid: %b1zm!", User.CheckPasswordFormat( "%b1zm!" ) );
				Assertion.Assert( "Password format should be valid: 1buzm!", User.CheckPasswordFormat( "1buzm!" ) );
				Assertion.Assert( "Password format should be valid: buzm123", User.CheckPasswordFormat( "buzm123" ) );
				Assertion.Assert( "Password format should be valid: buzm@123buzm", User.CheckPasswordFormat( "buzm@123buzm" ) );
			}

			[Test] public void CheckFormatsTest()
			{
				string resultMsg;
				User user = new User( );
				user.Login = "okarim";
				user.Email = "okarim@buzm.com";
				user.Password = "okarim1";
				
				// check with valid profile fields
				Assertion.Assert( "Valid user profile failed check.", user.CheckProfileFormats( out resultMsg ) );

				// check with invalid profile fields
				user.Login = "okarim!"; // invalid login
				Assertion.Assert( "Invalid user profile passed check.", !user.CheckProfileFormats( out resultMsg ) );
				user.Email = "okarim@com"; // invalid email
				Assertion.Assert( "Invalid user profile passed check.", !user.CheckProfileFormats( out resultMsg ) );
				user.Password = "okarim"; // invalid password
				Assertion.Assert( "Invalid user profile passed check.", !user.CheckProfileFormats( out resultMsg ) );
			}

			[Test] public void CloneIdentityTest()
			{
				User user = new User( );
				user.Login = "okarim";				
				user.Password = "okarim1";
				user.Email = "okarim@buzm.com";
				User clone = user.CloneIdentity();

				// compare cloned users profile fields
				Assertion.AssertEquals( "Cloned user should have same login.", "okarim", clone.Login );
				Assertion.AssertEquals( "Cloned user should have same password.", "okarim1", clone.Password );
				Assertion.AssertEquals( "Cloned user should not have same email.", "", clone.Email );
			}
			
			[Test] public void HiveGetSetRemoveTest()
			{
				// create dummy user
				User user = new User( );
				
				// get non-existent hive
				XmlNode getResult = user.GetHive( );
				Assert.IsNull( getResult, "Get non-existent hive" );

				// get non-existent hive
				getResult = user.GetHive( "0" );
				Assert.IsNull( getResult, "Get non-existent hive" );
				
				// save three hives and update one to create loop sequence
				XmlNode newHive = user.SetHive( "<hive><name>Hive0</name><guid>0</guid></hive>" );
				Assert.IsNotNull( newHive, "Set Hive failed for Hive0" );
				newHive = user.SetHive( "<hive><name>Hive9</name><guid>1</guid></hive>" );				
				Assert.IsNotNull( newHive, "Set Hive failed for Hive9" );
				newHive = user.SetHive( "<hive><name>Hive2</name><guid>2</guid></hive>" );
				Assert.IsNotNull( newHive, "Set Hive failed for Hive2" );
				newHive = user.SetHive( "<hive><name>Hive1</name><guid>1</guid></hive>" );
				Assert.IsNotNull( newHive, "Set Hive failed for Hive1" );
				
				// get first hive from user
				getResult = user.GetHive( );
				string hiveName = getResult.SelectSingleNode( "name" ).InnerText;
				Assert.AreEqual( "Hive0", hiveName, "Get first hive mismatch" );

				// try to remove non-existent hive
				getResult = user.RemoveHive( "50" );
				Assert.IsNull( getResult, "Tried to remove non-existent hive" );
				
				// try to remove hive with invalid guid
				getResult = user.RemoveHive( "'guid'" );
				Assert.IsNull( getResult, "Tried to remove hive with invalid guid" );

				// try to remove last hive
				getResult = user.RemoveHive( "2" );
				string resultGuid = getResult.SelectSingleNode( "guid" ).InnerText;	

				// verify that hive was removed correctly
				Assert.IsNotNull( getResult, "Failed to remove hive 2 from collection" );
				Assert.IsNull( getResult.ParentNode, "Removed hive still has parent" );
				Assert.AreEqual( "2", resultGuid, "Incorrect hive was deleted" );
				
				// get all hives from user
				int count = 0; // loop counter
				XmlNodeList hives = user.Hives;
				foreach( XmlNode hive in hives )
				{
					// get the hive again to confirm accuracy of the GetHive method
					getResult = user.GetHive( hive.SelectSingleNode( "guid" ).InnerText );
					hiveName = getResult.SelectSingleNode( "name" ).InnerText;
					Assert.AreEqual( "Hive" + count.ToString(), hiveName, "Get hive mismatch" );	
					count++; // increment counter for next hive name
				}

				// verify correct hive count after add/update/delete
				Assert.AreEqual( 2, count, "Incorrect hive count" );
			}

			[Test] public void CloneHivesTest()
			{
				// create master user
				User fromUser = new User();
				fromUser.Login = "fromUser";

				// create destination user
				User toUser = new User();
				toUser.Login = "toUser";

				// clone master sans hives
				toUser.CloneHives( fromUser );
				Assert.AreEqual( 0, toUser.Hives.Count, "Expected no hives from master" );
				
				// add empty hive sans config options to master
				fromUser.SetHive( "<hive><guid>guid</guid><name>name</name></hive>" );
				
				toUser.CloneHives( fromUser ); // clone single empty hive
				Assert.AreEqual( 1, toUser.Hives.Count, "Expected one empty hive from master" );

				XmlNode hiveNode = toUser.GetHive(); // get first and only hive
				Assert.AreEqual( "name", hiveNode.SelectSingleNode( "name" ).InnerText, "Incorrect name for empty hive" );
				Assert.AreNotEqual( "guid", hiveNode.SelectSingleNode( "guid" ).InnerText, "Guid unchanged for empty hive" );

				// reset test users
				fromUser = fromUser.CloneIdentity();
				toUser = toUser.CloneIdentity();
				
				// setup base hive config with all options
				string hiveConfig = "<hive><guid>g{0}</guid><name>n{0}</name><host>h{0}</host>"
				+ "<owner /><feeds /><members /><memberDate /><createDate>d{0}</createDate>"
				+ "<inviteText>i{0}</inviteText><skin><guid>s{0}</guid></skin></hive>";

				// save dummy hives to master user profile
				fromUser.SetHive( String.Format( hiveConfig, "0" ) );
				fromUser.SetHive( String.Format( hiveConfig, "1" ) );

				// add member to first hive
				fromUser.SetMember( "g0", "<user><guid>m0</guid><login>l0</login></user>" );

				// add feeds to second hive
				fromUser.SetFeed( "g1", "<feed><guid>fg0</guid><name>fn0</name></feed>" );
				fromUser.SetFeed( "g1", "<feed><guid>fg1</guid><name>fn1</name></feed>" );

				toUser.CloneHives( fromUser ); // clone master hives
				Assert.AreEqual( 2, toUser.Hives.Count, "Expected cloned hives from master" );
				Assert.AreEqual( "toUser", toUser.Login, "Login corrupted after cloning" );

				int[] feedCount = new int[2] { 0, 2 }; // feeds per hive
				XmlNodeList hives = toUser.Hives; // get cloned hives

				for( int x = 0; x < 2; x++ ) // iterate to validate configuration
				{
					Assert.AreNotEqual( "g" + x, hives[x].SelectSingleNode( "guid" ).InnerText, "Guid unchanged for hive " + x );
					Assert.AreNotEqual( "d" + x, hives[x].SelectSingleNode( "createDate" ).InnerText, "Create date unchanged for hive " + x );

					Assert.AreEqual( "n" + x, hives[x].SelectSingleNode( "name" ).InnerText, "Name corrupted for hive " + x );
					Assert.AreEqual( "i" + x, hives[x].SelectSingleNode( "inviteText" ).InnerText, "Invite text corrupted for hive " + x );
					Assert.AreEqual( "s" + x, hives[x].SelectSingleNode( "skin/guid" ).InnerText, "Skin corrupted for hive " + x );					

					Assert.IsEmpty( hives[x].SelectSingleNode( "host" ).InnerText, "Host not empty for hive " + x );
					Assert.IsEmpty( hives[x].SelectSingleNode( "owner" ).InnerText, "Owner not empty for hive " + x );
					Assert.IsEmpty( hives[x].SelectSingleNode( "memberDate" ).InnerText, "Member date not empty for hive " + x );
					Assert.IsEmpty( hives[x].SelectSingleNode( "members" ).InnerXml, "Members not empty for hive " + x );					

					XmlNodeList feeds = toUser.GetFeeds( hives[x].SelectSingleNode( "guid" ).InnerText );
					Assert.AreEqual( feedCount[x], feeds.Count, "Incorrect cloned feeds for hive " + x );

					for( int y = 0; y < feedCount[x]; y++ ) // iterate to validate feed configuration
					{
						Assert.AreNotEqual( "fg" + y, feeds[y].SelectSingleNode( "guid" ).InnerText, "Guid unchanged for feed " + y );
						Assert.AreEqual( "fn" + y, feeds[y].SelectSingleNode( "name" ).InnerText, "Name corrupted for feed " + y );
					}
				}
			}

			[Test]
			public void FeedGetSetRemoveTest()
			{
				// create dummy user
				User user = new User( );
				
				// save two hives to add feeds within
				user.SetHive( "<hive><name>Hive0</name><guid>0</guid><feeds></feeds></hive>" );
				user.SetHive( "<hive><name>Hive1</name><guid>1</guid><feeds></feeds></hive>" );
				
				// add two feeds to the first hive and update one
				XmlNode newFeed = user.SetFeed( "0", "<feed><name>feed00</name><guid>00</guid></feed>" );
				Assert.IsNotNull( newFeed, "Set feed failed for feed00" );
				newFeed = user.SetFeed( "0", "<feed><name>feed09</name><guid>01</guid></feed>" );
				Assert.IsNotNull( newFeed, "Set feed failed for feed09" );
				newFeed = user.SetFeed( "0", "<feed><name>feed01</name><guid>01</guid></feed>" );
				Assert.IsNotNull( newFeed, "Set feed failed for feed01" );
				
				// add three feeds to the second hive and update one
				newFeed = user.SetFeed( "1", "<feed><name>feed10</name><guid>10</guid></feed>" );
				Assert.IsNotNull( newFeed, "Set feed failed for feed10" );
				newFeed = user.SetFeed( "1", "<feed><name>feed19</name><guid>11</guid></feed>" );
				Assert.IsNotNull( newFeed, "Set feed failed for feed19" );
				newFeed = user.SetFeed( "1", "<feed><name>feed12</name><guid>12</guid></feed>" );
				Assert.IsNotNull( newFeed, "Set feed failed for feed12" );
				newFeed = user.SetFeed( "1", "<feed><name>feed11</name><guid>11</guid></feed>" );
				Assert.IsNotNull( newFeed, "Set feed failed for feed11" );
				
				// get feed from non-existent hive
				XmlNode getResult = user.GetFeed( "2", "0" );
				Assert.IsNull( getResult, "Get feed from non-existent hive" );
				
				// get non-existent feed from hive
				getResult = user.GetFeed( "0", "50" );
				Assert.IsNull( getResult, "Get non-existent feed from hive" );
				
				int hiveCount = 0;
				XmlNodeList hives = user.Hives;
				foreach( XmlNode hive in hives )
				{
					int feedCount = 0;
					string hiveGuid = hive.SelectSingleNode( "guid" ).InnerText;
					XmlNodeList feeds = user.GetFeeds( hiveGuid );
					foreach( XmlNode feed in feeds )
					{
						// get the feed again to confirm accuracy of the GetFeed method
						getResult = user.GetFeed( hiveGuid, feed.SelectSingleNode( "guid" ).InnerText );
						string actualFeedName = getResult.SelectSingleNode( "name" ).InnerText;
						string expectFeedName = "feed" + hiveCount.ToString() + feedCount.ToString();
						Assert.AreEqual( expectFeedName, actualFeedName, "Get feed mismatch" );	
						feedCount++; // increment counter for next feed name
					}
					hiveCount++; // increment counter for next feed name
				}

				// remove feed from non-existent hive
				getResult = user.RemoveFeed( "2", "0" );
				Assert.IsNull( getResult, "Remove feed from non-existent hive" );
				
				// remove non-existent feed from hive
				getResult = user.RemoveFeed( "0", "50" );
				Assert.IsNull( getResult, "Remove non-existent feed from hive" );

				// remove one feed from first hive
				getResult = user.RemoveFeed( "0", "01" );
				string resultGuid = getResult.SelectSingleNode( "guid" ).InnerText;				
				
				Assert.IsNotNull( getResult, "Failed to remove feed 01 from first hive" );
				Assert.IsNull( getResult.ParentNode, "Removed feed still has parent" );
				Assert.AreEqual( "01", resultGuid, "Incorrect feed was deleted" );

				// try to get feed again from hive
				getResult = user.GetFeed( "0", "01" );
				Assert.IsNull( getResult, "Found deleted feed 01 in hive" );

				// get remaining feed from first hive
				XmlNodeList liveFeeds = user.GetFeeds( "0" );
				Assert.AreEqual( 1, liveFeeds.Count, "Incorrect feed count after delete" );
				string liveFeedGuid = liveFeeds[0].SelectSingleNode( "guid" ).InnerText;
				Assert.AreEqual( "00", liveFeedGuid, "Incorrect feed after delete" );

				// remove second feed from first hive
				getResult = user.RemoveFeed( "0", "00" );
				resultGuid = getResult.SelectSingleNode( "guid" ).InnerText;				
				
				Assert.IsNotNull( getResult, "Failed to remove feed 00 from first hive" );
				Assert.IsNull( getResult.ParentNode, "Removed feed still has parent" );
				Assert.AreEqual( "00", resultGuid, "Incorrect feed was deleted" );
			}

			[Test] public void MemberGetSetRemoveTest()
			{
				// create dummy user
				User user = new User( );
				
				// save two hives to add members within
				user.SetHive( "<hive><name>Hive0</name><guid>0</guid><members></members></hive>" );
				user.SetHive( "<hive><name>Hive1</name><guid>1</guid><members></members></hive>" );
				
				User member = new User( );
				member.Login = "member00";
				member.Guid = "00"; 
				
				// add two members to the first hive and update one
				XmlNode newMember = user.SetMember( "0", member.ToXmlString() );
				Assert.IsNotNull( newMember, "Set member failed for member00" );
				
				member.Guid = "01";
				member.Login = "member09"; // set mismatched login
				newMember = user.SetMember( "0", member.ToXmlString( ) );
				Assert.IsNotNull( newMember, "Set member failed for member09" );
				
				member.Login = "member01"; // update to matched login
				newMember = user.SetMember( "0", member.ToXmlString( ) );
				Assert.IsNotNull( newMember, "Set member failed for member01" );
				
				member.Guid = "10";
				member.Login = "member10"; // add member to second hive
				newMember = user.SetMember( "1", member.ToXmlString( ) );
				Assert.IsNotNull( newMember, "Set member failed for member10" );
				
				// get member from non-existent hive
				XmlNode getResult = user.GetMember( "2", "01" );
				Assert.IsNull( getResult, "Get member from non-existent hive" );
				
				// get non-existent member from hive
				getResult = user.GetMember( "0", "50" );
				Assert.IsNull( getResult, "Get non-existent member from hive" );
				
				User memUser;
				SafeXmlDoc memDoc;
				int hiveCount = 0;
				XmlNodeList hives = user.Hives;
				foreach( XmlNode hive in hives )
				{
					int memberCount = 0;
					string hiveGuid = hive.SelectSingleNode( "guid" ).InnerText;
					XmlNodeList members = user.GetMembers( hiveGuid );
					foreach( XmlNode memNode in members )
					{
						// get member again to check GetMember
						memDoc = new SafeXmlDoc( memNode.OuterXml );
						memUser = new User( memDoc ); // instantiate member
						getResult = user.GetMember( hiveGuid, memUser.Guid );

						string actualMemberName = getResult.SelectSingleNode( "login" ).InnerText;
						string expectMemberName = "member" + hiveCount.ToString() + memberCount.ToString();
						Assert.AreEqual( expectMemberName, actualMemberName, "Get member mismatch" );	
						memberCount++; // increment counter for next member name
					}
					hiveCount++; // increment counter for next member name
				}

				// remove member from non-existent hive
				getResult = user.RemoveMember( "2", "00" );
				Assert.IsNull( getResult, "Remove member from non-existent hive" );
				
				// remove non-existent member from hive
				getResult = user.RemoveMember( "0", "50" );
				Assert.IsNull( getResult, "Remove non-existent member from hive" );

				// remove one member from first hive
				getResult = user.RemoveMember( "0", "01" );
				string resultGuid = getResult.SelectSingleNode( "guid" ).InnerText;				
				
				Assert.IsNotNull( getResult, "Failed to remove member 01 from first hive" );
				Assert.IsNull( getResult.ParentNode, "Removed member still has parent" );
				Assert.AreEqual( "01", resultGuid, "Incorrect member was deleted" );

				// try to get member again from hive
				getResult = user.GetMember( "0", "01" );
				Assert.IsNull( getResult, "Found deleted member 01 in hive" );

				// remove member by login from non-existent hive
				getResult = user.RemoveMemberByLogin( "2", "member10" );
				Assert.IsNull( getResult, "Remove member by login from non-existent hive" );
				
				// remove non-existent member by login from hive
				getResult = user.RemoveMemberByLogin( "1", "member50" );
				Assert.IsNull( getResult, "Remove non-existent member by login from hive" );

				// remove member with illegal login from hive
				getResult = user.RemoveMemberByLogin( "1", "me'mbe'r50" );
				Assert.IsNull( getResult, "Remove member with illegal login from hive" );

				// remove one member by login from first hive
				getResult = user.RemoveMemberByLogin( "1", "member10" );
				resultGuid = getResult.SelectSingleNode( "guid" ).InnerText;				
				
				Assert.IsNotNull( getResult, "Failed to remove member 10 by login from second hive" );
				Assert.IsNull( getResult.ParentNode, "Removed member still has parent" );
				Assert.AreEqual( "10", resultGuid, "Incorrect member was deleted" );

				// try to get member again from hive
				getResult = user.GetMember( "1", "10" );
				Assert.IsNull( getResult, "Found deleted member 10 in hive" );

				// get remaining member from first hive
				XmlNodeList liveMembers = user.GetMembers( "0" );
				Assert.AreEqual( 1, liveMembers.Count, "Incorrect member count in first hive after delete" );
				string liveMemberGuid = liveMembers[0].SelectSingleNode( "guid" ).InnerText;
				Assert.AreEqual( "00", liveMemberGuid, "Incorrect member in first hive after delete" );
				
				liveMembers = user.GetMembers( "1" ); // confirm that no members are left in second hive
				Assert.AreEqual( 0, liveMembers.Count, "Incorrect member count in second hive after delete" );
			}
		}

		#endif
		#endregion
	}

	public enum UserLoginState : int
	{
		None,
		Auto,
		Silent,
		Manual
	}
}
