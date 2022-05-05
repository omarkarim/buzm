using System;
using System.IO;
using System.Xml;
using Buzm.Utility;
using Buzm.Schemas;
using NUnit.Framework;
using System.Reflection;
using System.Diagnostics;
using System.Collections;

namespace Buzm.Register
{
	/// <summary>Encapsulates the storage of 
	/// user data. Currently file-based but can
	/// easily be migrated to an RDBMS </summary>
	public class Registry
	{
		private string m_UsersFolder;
		private int m_MaxFileNameLength;

		private string m_DefaultProfile;
		private string m_RegisterMessage;

		// xpath to registry configuration settings
		private const string CONFIG_PATH = "preferences/registry/";
					
		public Registry( )
		{
			m_UsersFolder = Config.GetFolderValue( CONFIG_PATH + "usersFolder" );
			m_DefaultProfile = Config.GetValue( CONFIG_PATH + "defaultProfile" );

			m_RegisterMessage = Config.GetOuterXml( CONFIG_PATH + "messages/message[guid='register']" );
			m_MaxFileNameLength = 50; // TODO: Check for FAT file system 8.3 name format
		}

		#region Request Routing Methods

		/// <summary>Applies the specified action against the user</summary>
		/// <param name="remoteUser">The remote user object to process</param>
		/// <param name="action">The action that should be applied to user</param>
		/// <param name="resultMsg">The reason the process could have failed</param>
		/// <returns>RegistryResult indicating process success or source of failure</returns>
		public RegistryResult ProcessUserAction( ref User remoteUser, RegistryAction action, out string resultMsg )
		{
			try // to process the remote user request based on RegistryAction type
			{	
				switch( action ) // any actions that require authorization are delegated
				{
					case RegistryAction.LoginUser: return LoginUser( ref remoteUser, true, true, out resultMsg ); 
					case RegistryAction.InsertUser: return InsertUser( remoteUser, out resultMsg ); 
					default: return AuthorizeUserAction( ref remoteUser, action, out resultMsg );
				}
			}
			catch( Exception e )
			{
				Log.Write( action.ToString() + " error", TraceLevel.Error, "Registry.ProcessUserAction", e );
				resultMsg = "Server could not process request - Please try again later.";
				return RegistryResult.RegistryError; // registry was likely at fault
			}
		}

		/// <summary>Checks the login credentials of the user before executing action request </summary>
		private RegistryResult AuthorizeUserAction( ref User remoteUser, RegistryAction action, out string resultMsg )
		{
			User localUser = remoteUser.CloneIdentity(); // copy user credentials
			RegistryResult result = LoginUser( ref localUser, true, out resultMsg );
			if( result == RegistryResult.Success ) // if the login attempt succeeded
			{	
				switch( action ) // any actions that are Hive specific are delegated 
				{
					case RegistryAction.UpdateUser: return RegistryResult.UnsupportedRequest;
					case RegistryAction.DeleteUser: return RegistryResult.UnsupportedRequest;
					default: return ProcessHiveAction( ref remoteUser, localUser, action, out resultMsg );
				}	
			}
			else // login verification failed
			{
				resultMsg = "Could not verify Buzm user: " + resultMsg;
				return result; // return result code from login function
			}
		}

		/// <summary>Extracts the Hive for which the action is being requested prior to processing </summary>
		private RegistryResult ProcessHiveAction( ref User remoteUser, User localUser, RegistryAction action, out string resultMsg )
		{
			XmlNode hiveNode = remoteUser.GetHive();
			if( hiveNode != null ) // if hive exists
			{
				XmlNode hiveGuidNode = hiveNode.SelectSingleNode( "guid" );
				if( hiveGuidNode != null ) // if a hive guid was provided
				{	
					string hiveGuid = hiveGuidNode.InnerText;					
					switch( action ) // process action for Hive
					{
						case RegistryAction.InsertHive:	 return InsertHive( localUser, hiveGuid, hiveNode, out resultMsg );
						case RegistryAction.DeleteHive:	 return DeleteHive( localUser, hiveGuid, true, out resultMsg );
						case RegistryAction.InsertFeeds: return InsertFeeds( remoteUser, localUser, hiveGuid, out resultMsg );
						case RegistryAction.DeleteFeeds: return DeleteFeeds( remoteUser, localUser, hiveGuid, out resultMsg );
						case RegistryAction.InsertMembers: return InsertMembers( remoteUser, localUser, hiveGuid, hiveNode, out resultMsg );
						case RegistryAction.DeleteMembers: return DeleteMembers( remoteUser, localUser, hiveGuid, true, true, out resultMsg );
						case RegistryAction.AcceptInvite: return AcceptInvite( ref remoteUser, localUser, hiveGuid, hiveNode, out resultMsg );						
						default: resultMsg = "Request is not supported by this server."; return RegistryResult.UnknownRequest;
					}
				}
			}
			resultMsg = "Please specify a Hive for the transaction.";
			return RegistryResult.ClientError;
		}

		#endregion
	
		#region Account Management Methods

		private RegistryResult InsertUser( User user, out string resultMsg )
		{
			// check if all user input fields are legal
			if( user.CheckProfileFormats( out resultMsg ) )
			{
				string login = user.Login;
				if( login.Length <= m_MaxFileNameLength )
				{
					// check if the account name is available
					if( !File.Exists( m_UsersFolder + login ) )
					{
						// load user profile with default hives
						CloneUserProfile( m_DefaultProfile, user );

						// save user data to disk in the configured registry folder
						if( user.SaveToDisk( m_UsersFolder + login ) )
						{
							if( !String.IsNullOrEmpty( m_RegisterMessage ) )
								user.SetMessage( m_RegisterMessage );

							// user account creation is complete
							return RegistryResult.Success;
						}
						else
						{
							// SaveToDisk will log the actual error locally - send generic message to client
							resultMsg = "Could not complete the registration process - Please try again later.";
							return RegistryResult.RegistryError; // indicate that the registry was at fault
						}
					} 
					else resultMsg = "The Buzm ID you chose is already in use - Please try a different one.";
				}
				else resultMsg = "Your Buzm ID must be less than " + m_MaxFileNameLength.ToString() + " characters.";
			}
			// TODO: Move hardcoded result messages to external resource file
			return RegistryResult.UserError; // failed because of bad user input
		}

		private RegistryResult LoginUser( ref User user, bool checkPassword, out string resultMsg )
		{				return LoginUser( ref user, checkPassword, false, out resultMsg ); /* backward compatible */ }
		
		private RegistryResult LoginUser( ref User user, bool checkPassword, bool processHosts, out string resultMsg )
		{
			resultMsg = String.Empty;
			string login = user.Login;
			string userFile = m_UsersFolder + login;

			// check if provided login is valid
			if( User.CheckLoginFormat( login ) )
			{
				// check if account exists 
				if( File.Exists( userFile ) )
				{
					// TODO: Add one-way password hash for user privacy
					User regUser = new User( userFile ); // load user profile
					if( !checkPassword || ( user.Password == regUser.Password ) ) 
					{
						if( processHosts ) LoadHostedHiveConfig( regUser );
						user = regUser; // set reference param
						return RegistryResult.Success;
					}
				}
			}
			
			resultMsg = "The Buzm ID and password you entered do not match - Please try again";
			return RegistryResult.UserError; // login attempt failed if we reached this point
		}

		/// <summary>Saves changes made to an authenticated user. The
		/// method fails if User was not loaded from this Registry since
		/// it will be unaware of the save location in that case.</summary>
		private RegistryResult SaveUser( User localUser, out string resultMsg )
		{
			if( localUser.SaveToDisk() ) // save updated profile
			{
				resultMsg = ""; // set empty result
				return RegistryResult.Success;
			}
			else
			{
				resultMsg = "Could not save profile - Please try again later."; 
				return RegistryResult.RegistryError;
			}
		}

		private void CloneUserProfile( string fromUserLogin, User toUser )
		{
			if( !String.IsNullOrEmpty( fromUserLogin ) && ( toUser != null ) )
			{
				User fromUser = new User();
				fromUser.Login = fromUserLogin;

				string resultMsg = String.Empty; // load origin user profile from disk
				if( LoginUser( ref fromUser, false, false, out resultMsg ) == RegistryResult.Success )
				{
					toUser.CloneHives( fromUser ); // with guids changed
				}
			}
		}

		#endregion

		#region Hive Management Methods

		private RegistryResult InsertHive( User localUser, string hiveGuid, XmlNode hiveNode, out string resultMsg )
		{												
			// if hive does not exist in the profile
			if( localUser.GetHive( hiveGuid ) == null )
			{
				// add new hive configuration to the user profile
				XmlNode localHive = localUser.SetHive( hiveNode.OuterXml );				
				if( localHive != null )
				{
					// extract new hive members and send out invites 
					XmlNodeList members = localUser.GetMembers( hiveGuid );
					if( ( members != null ) && ( members.Count > 0 ) )
					{
						// TODO: Save the member invites that were sent successfully even if some failed?
						RegistryResult result = SendHiveInvites( localUser, members, hiveNode, out resultMsg );
						if( result != RegistryResult.Success ) return result;
					}
					
					// save inserted hive in local profile
					return SaveUser( localUser, out resultMsg );
				}
				else
				{
					resultMsg = "Could not create the Hive - Please check Hive properties and try again.";
					return RegistryResult.ClientError;								
				}
			}
			else
			{
				resultMsg = "This Hive already exists in your profile - Please reconnect to load it";
				return RegistryResult.ClientError;
			}
		}

		private RegistryResult DeleteHive( User localUser, string hiveGuid, bool updateHost, out string resultMsg )
		{
			XmlNode hiveNode = localUser.GetHive( hiveGuid );
			if( hiveNode != null ) // if hive exists in profile
			{	
				if( updateHost ) // update host profile if required
				{
					XmlNode hostNode = hiveNode.SelectSingleNode( "host" );
					if( hostNode != null ) // host node exists in local profile
					{
						string hostLogin = hostNode.InnerText; // and is not local user
						if( ( hostLogin != String.Empty ) && ( hostLogin != localUser.Login ) )
						{
							User hostUser = new User(); // initialize and login host user and try to 
							hostUser.Login = hostLogin; // delete member registration from host profile
							if( LoginUser( ref hostUser, false, out resultMsg ) == RegistryResult.Success )
							{
								XmlNode remHost = hostUser.RemoveMemberByLogin( hiveGuid, localUser.Login );
								if( remHost != null ) SaveUser( hostUser, out resultMsg ); // not fatal
							}
						}
					}
				}
				// delete hive members recursively and then remove hive from local user profile
				DeleteMembers( localUser, localUser, hiveGuid, false, false, out resultMsg );				
				XmlNode remHive = localUser.RemoveHive( hiveGuid ); 
				if( remHive == null ) // if delete failed
				{
					resultMsg = "Hive could not be deleted. Please try again later";
					return RegistryResult.ServerError;
				}			
				else return SaveUser( localUser, out resultMsg ); // save to disk
			}
			else
			{
				resultMsg = "This Hive has already been removed.";
				return RegistryResult.Success;
			}
		}

		/// <summary>Checks which user hives are hosted and merges the
		/// shared configuration of the host with the user copy</summary>
		private void LoadHostedHiveConfig( User user )
		{
			string userLogin = user.Login;
			string resultMsg = String.Empty;
			Hashtable hostCache = new Hashtable();
			
			XmlNodeList userHiveNodes = user.Hives;
			if( userHiveNodes != null ) // got user hives
			{
				// iterate user hives and check host config
				foreach( XmlNode userHiveNode in userHiveNodes )
				{	
					XmlNode hostNode = userHiveNode.SelectSingleNode( "host" );
					if( hostNode != null ) // host node exists in config
					{
						string hostLogin = hostNode.InnerText; // if user is not host
						if( ( hostLogin != String.Empty ) && ( hostLogin != userLogin ) )
						{
							User host = hostCache[hostLogin] as User;
							if( host == null ) // host not in cache
							{
								host = new User();
								host.Login = hostLogin;
								
								// load profile of the hive host from disk
								if( LoginUser( ref host, false, false, out resultMsg ) == RegistryResult.Success ) 
									hostCache.Add( hostLogin, host ); // save to cache for repeat use
								else continue; // host login failed so skip this hive
							}
		
							// merge hives in user and host profile based on their guid
							XmlNode userGuidNode = userHiveNode.SelectSingleNode( "guid" );
							if( userGuidNode != null ) // guid exists in user hive config
							{
								XmlNode hostHiveNode = host.GetHive( userGuidNode.InnerText );
								if( hostHiveNode != null ) // user hive exists in host profile
									MergeHiveConfig( hostLogin, hostHiveNode, userHiveNode );
							}
						}
					}
				}
			}
		}

		/// <summary>Copies global hive config from host into local user hive config and
		/// overrides elements that are specific to the user such as the host field</summary>
		private void MergeHiveConfig( string hostLogin, XmlNode hostHiveNode, XmlNode userHiveNode )
		{
			try // to overwrite user hive xml with host version
			{ 	
				// TODO: investigate more efficient copy code
				userHiveNode.InnerXml = hostHiveNode.InnerXml;
		
				// reset host in user hive xml to original value
				XmlNode hostNode = userHiveNode.SelectSingleNode( "host" );
				if( hostNode != null ) hostNode.InnerText = hostLogin;
			}
			catch { /* ignore errors since user hive node is unchanged */  }
		}

		private RegistryResult InsertMembers( User remoteUser, User localUser, string hiveGuid, XmlNode hiveNode, out string resultMsg )
		{
			// if hive exists in local user profile
			if( localUser.GetHive( hiveGuid ) != null )
			{
				// extract new hive members and send out invites 
				XmlNodeList members = remoteUser.GetMembers( hiveGuid );
				if( ( members != null ) && ( members.Count > 0 ) )
				{
					RegistryResult inviteResult = SendHiveInvites( localUser, members, hiveNode, true, out resultMsg );
					switch( inviteResult ) // process user based on result code
					{
						case RegistryResult.Success: // save user and return							
							return SaveUser( localUser, out resultMsg );
						
						case RegistryResult.Warning: // preserve warning message
							
							string saveResultMsg; // save user with members to disk
							RegistryResult saveResult = SaveUser( localUser, out saveResultMsg );

							if( saveResult == RegistryResult.Success ) return inviteResult;
							else // return failure message from save
							{
								resultMsg = saveResultMsg;
								return saveResult;
							}

						default: // all invites failed
							return inviteResult; 
					}
				}
				else // invite request is empty
				{
					resultMsg = String.Empty;
					return RegistryResult.Success;
				}				
			}
			else // hive guid not found in local user profile
			{
				resultMsg = "Server could not find invite Hive - Please login again to refresh your session.";
				return RegistryResult.RegistryError;
			}
		}

		private RegistryResult DeleteMembers( User remoteUser, User localUser, string hiveGuid, bool saveChanges, bool abortOnError, out string resultMsg )
		{
			// if hive exists in local user profile
			if( localUser.GetHive( hiveGuid ) != null )
			{
				resultMsg = String.Empty; // iterate to delete hive members
				foreach( XmlNode memNode in remoteUser.GetMembers( hiveGuid ) )
				{					
					XmlNode guidNode = memNode.SelectSingleNode( "guid" );
					if( guidNode != null ) // if remote member guid exists
					{
						string memGuid = guidNode.InnerText;
						XmlNode locMemNode = localUser.GetMember( hiveGuid, memGuid );
						if( locMemNode != null ) // if member exists in local profile
						{
							SafeXmlDoc memDoc = new SafeXmlDoc( locMemNode.OuterXml );
							User memUser = new User( memDoc ); // create temporary user obj
							if( memUser.Login != String.Empty ) // if member has joined this hive
							{
								// try loading member profile and if that succeeds try recursive hive delete
								if( ( LoginUser( ref memUser, false, out resultMsg ) != RegistryResult.Success ) ||
									( DeleteHive( memUser, hiveGuid, false, out resultMsg ) != RegistryResult.Success ) )
								{
									if( abortOnError ) // the caller can choose to ignore errors during the delete process
									{
										resultMsg = "Could not remove member: " + memUser.Login + " - Please try again later.";
										return RegistryResult.RegistryError;
									}
								}
							}
							// remove active and inactive members from local profile
							XmlNode remMember = localUser.RemoveMember( hiveGuid, memGuid );
							if( remMember == null ) // member removal process failed
							{
								resultMsg = "Could not remove member - Please reconnect and try again.";
								return RegistryResult.RegistryError;
							}
						}
					}
					else // remote member does not contain a guid node
					{
						resultMsg = "Could not remove member - Please login again to refresh your profile.";
						return RegistryResult.ClientError;
					}
				}
				// if needed, save user profile without the deleted members
				if( saveChanges ) return SaveUser( localUser, out resultMsg );
				else return RegistryResult.Success; // with empty resultMsg
			}
			else // hive guid not found in local user profile
			{
				resultMsg = "Could not find Hive - Please login again to refresh your profile.";
				return RegistryResult.RegistryError;
			}
		}

		private RegistryResult InsertFeeds( User remoteUser, User localUser, string hiveGuid, out string resultMsg )
		{
			// if hive exists in local user profile
			if( localUser.GetHive( hiveGuid ) != null )
			{
				// iterate the feeds requested for the specified hive
				foreach( XmlNode feed in remoteUser.GetFeeds( hiveGuid ) )
				{
					// add feed configuration to the registered user profile
					XmlNode localFeed = localUser.SetFeed( hiveGuid, feed.OuterXml );
					if( localFeed == null ) // feed was not parsed successfully
					{
						resultMsg = "Could not process the feed request - Please check feed properties and try again.";
						return RegistryResult.ClientError;
					}
				}

				// save inserted feeds in local profile		
				return SaveUser( localUser, out resultMsg );
			}
			else // hive guid not found in local user profile
			{
				resultMsg = "Server could not find the requested Hive - Please login again to refresh your session.";
				return RegistryResult.RegistryError;
			}
		}

		private RegistryResult DeleteFeeds( User remoteUser, User localUser, string hiveGuid, out string resultMsg )
		{
			// if hive exists in local user profile
			if( localUser.GetHive( hiveGuid ) != null )
			{
				// iterate the feeds to be deleted from specified hive
				foreach( XmlNode feed in remoteUser.GetFeeds( hiveGuid ) )
				{
					// check if deleted feed exists in local profile
					XmlNode guidNode = feed.SelectSingleNode( "guid" );
					if( guidNode != null )
					{
						string feedGuid = guidNode.InnerText;
						if( localUser.GetFeed( hiveGuid, feedGuid ) != null )
						{
							XmlNode remFeed = localUser.RemoveFeed( hiveGuid, feedGuid );
							if( remFeed == null ) // feed was not removed successfully
							{
								resultMsg = "Could not process feed - Please reconnect and try again.";
								return RegistryResult.RegistryError;
							}
						}
					}
					else // remote feed does not contain a guid node
					{
						resultMsg = "Could not process feed - Please check feed properties and try again.";
						return RegistryResult.ClientError;
					}
				}

				// save local profile sans deleted feeds
				return SaveUser( localUser, out resultMsg );
			}
			else // hive guid not found in local user profile
			{
				resultMsg = "Could not find the Hive for this feed - Please login again to refresh your profile.";
				return RegistryResult.RegistryError;
			}
		}

		/// <summary>Adds a Hive to the user profile based on an invite sent by another Buzm 
		/// user. The inviter identity is extracted from the host property of the Hive </summary>
		private RegistryResult AcceptInvite( ref User remoteUser, User localUser, string hiveGuid, XmlNode hiveNode, out string resultMsg )
		{
			// if hive not already set in the profile
			if( localUser.GetHive( hiveGuid ) == null )
			{
				// create wrapper xml doc to get hive properties
				SafeXmlDoc hiveDoc = new SafeXmlDoc( hiveNode.OuterXml );
				
				User hostUser = new User(); // create user object to load host profile
				hostUser.Login = hiveDoc.GetInnerText( "/hive/host", "Registry.AcceptInvite" );

				// try to load host profile so we can verify that invitation is still valid
				if( LoginUser( ref hostUser, false, out resultMsg ) == RegistryResult.Success )
				{
					XmlNode hostHiveNode = hostUser.GetHive( hiveGuid );
					if( hostHiveNode != null ) // if the Hive still exists
					{						
						XmlNode hostMemberNode = hostUser.GetMember( hiveGuid, remoteUser.Guid );
						if( hostMemberNode != null ) // if the Hive invitation still exists
						{
							XmlNode hostSkinNode = hostHiveNode.SelectSingleNode ( "skin/guid" );
							XmlNode hostCreateDateNode = hostHiveNode.SelectSingleNode ( "createDate" );

							if( ( hostSkinNode != null ) && ( hostCreateDateNode != null ) ) // if hive settings exists
							{
								// try to set the Hive create date and local skin based on the configuration of the Hive host
								hiveDoc.SetInnerText( "/hive/createDate", hostCreateDateNode.InnerText, "Registry.AcceptInvite" );
								hiveDoc.SetInnerText( "/hive/skin/guid", hostSkinNode.InnerText, "Registry.AcceptInvite" );
								
								XmlNode regHiveNode = localUser.SetHive( hiveDoc.OuterXml );
								if( regHiveNode != null ) // if Hive registered successfully
								{									
									XmlNode memberLoginNode = hostMemberNode.SelectSingleNode( "login" );
									if( ( memberLoginNode != null ) && ( memberLoginNode.InnerText == String.Empty ) ) 
									{
										memberLoginNode.InnerText = localUser.Login; // set login in host
										if( SaveUser( hostUser, out resultMsg ) == RegistryResult.Success )
										{
											// save local user profile with new Hive subscription added
											if( SaveUser( localUser, out resultMsg ) == RegistryResult.Success )
											{
												// setup minimal user with new Hive only
												User regHiveUser = localUser.CloneIdentity();
												regHiveNode = regHiveUser.SetHive( hiveDoc.OuterXml );
												
												// merge additional elements from host hive config
												MergeHiveConfig( hostUser.Login, hostHiveNode, regHiveNode );
												
												remoteUser = regHiveUser; // set ref param
												return RegistryResult.Success;
											}
										}
									}
									else 
									{
										resultMsg = "This Hive invitation has already been used by another member.";
										return RegistryResult.UserError;
									}
								}
							}
							resultMsg = "Could not setup the Hive subscription - Please try again later.";
							return RegistryResult.RegistryError;
						}
						else 
						{
							resultMsg = "Your invitation to the Hive has expired.";
							return RegistryResult.UserError;	
						}
					}
					else 
					{
						resultMsg = "The Hive you were invited to is no longer active.";
						return RegistryResult.UserError;	
					}
				}
				else 
				{
					resultMsg = "The Buzm user that invited you to this Hive is longer active.";
					return RegistryResult.UserError;	
				}
			}
			else
			{
				resultMsg = "Your are already a member of the Hive.";
				return RegistryResult.UserError;
			}
		}

		private RegistryResult SendHiveInvites( User fromUser, XmlNodeList toUsers, XmlNode hive, out string resultMsg )
		{	return SendHiveInvites( fromUser, toUsers, hive, false, out resultMsg );	} // wrapper for legacy function calls
		
		private RegistryResult SendHiveInvites( User fromUser, XmlNodeList toUsers, XmlNode hive, bool updateFromUser, out string resultMsg )
		{												
			// create wrapper xml doc to get hive properties
			SafeXmlDoc hiveDoc = new SafeXmlDoc( hive.OuterXml );
			string hiveGuid = hiveDoc.GetInnerText( "/hive/guid", "Registry.SendHiveInvites" );
			string hiveName = hiveDoc.GetInnerText( "/hive/name", "Registry.SendHiveInvites" );
			string message = hiveDoc.GetInnerText( "/hive/inviteText", "Registry.SendHiveInvites" );
			
			int failureCount = 0; // number of failed user invites
			string failedEmails = ""; // email addresses of any failed invites
			UserInvite invite = new UserInvite( fromUser, hiveName, hiveGuid, message );

			foreach( XmlNode toUserNode in toUsers ) // loop to send invites
			{
				string toUserXml = toUserNode.OuterXml; 
				SafeXmlDoc toUserDoc = new SafeXmlDoc( toUserXml );
				
				User toUser = new User( toUserDoc ); // create temp user
				if( invite.Send( toUser ) ) // if send invite succeeded
				{
					if( !updateFromUser || // optionally set members
					  ( fromUser.SetMember( hiveGuid, toUserXml ) != null ) ) 
						continue; // process next hive invite
				}
				
				failureCount++; // send or set member failed
				failedEmails += " " + toUser.Email;
			}

			// check failed invitations and return an appropriate response
			if( failureCount > 0 ) // if one or more of the invitations failed
			{
				if( failureCount == toUsers.Count ) // if all invitations failed
				{
					resultMsg = "Could not send Hive invitations. Please try again later.";
					return RegistryResult.ServerError;
				}
				else
				{
					resultMsg = "Could not complete the following invites:" + failedEmails;
					return RegistryResult.Warning;
				}
			}
			else // all invites were sent - note that delivery is not guaranteed
			{  
				resultMsg = ""; // return empty message
				return RegistryResult.Success; 
			}
		}

		#endregion

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class RegistryTest
		{
			private User m_User;			
			ConsoleListener m_Listener;
			private string m_ResultMsg; 
			private Registry m_Registry;
			private RegistryResult m_Result;						
			private const int LARGE_HIVE_MEMBERS = 500;

			[SetUp] public void SetUp() 
			{ 
				// bind to NUnit console listener
				m_Listener = new ConsoleListener();				
				Trace.Listeners.Add( m_Listener );
				Log.TraceLevel = TraceLevel.Info;

				// Load local config file for the Buzm.Utility assembly
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly );

				// initialize local registry
				m_Registry = new Registry();
				m_Registry.m_DefaultProfile = null;
				m_Registry.m_RegisterMessage = null;

				// create test user
				m_User = new User();				
				m_User.Login = "buzmuser"; 
				m_User.Password = "password1";
				m_User.Email = "okarim@buzm.com";
			}

			[TearDown] public void TearDown() 
			{
				// delete user profile so the test can be run again
				File.Delete( m_Registry.m_UsersFolder + m_User.Login );

				// remove NUnit console listener
				Trace.Listeners.Remove( m_Listener );	
			}

			[Test] public void InsertUserTest()
			{
				// set a user name longer than NTFS filesystem limits
				m_User.Login = "omarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarimomarkarim";

				// create account with extremely long login name
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.UserError, m_Result, "Created account with invalid login: " + m_ResultMsg );

				// set normal login
				m_User.Login = "buzmuser"; 
				// set invalid password
				m_User.Password = "password";

				// create new user account with invalid password
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.UserError, m_Result, "Created account with invalid password: " + m_ResultMsg );

				// set normal password
				m_User.Password = "password1"; 
				
				// create new user account with valid data
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Account creation failed: " + m_ResultMsg );

				// ensure no default hives or messages were added
				Assert.AreEqual( 0, m_User.Hives.Count, "Unexpected hives with null default profile" );
				Assert.IsNull( m_User.GetMessage( "register" ), "Unexpected info from null default message" );

				// try to create the same account again
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.UserError, m_Result, "Tried to create same account again: " + m_ResultMsg );

				// delete profile so registration can be repeated
				File.Delete( m_Registry.m_UsersFolder + m_User.Login );
				
				// set invalid registration defaults
				m_Registry.m_DefaultProfile = "nonexistentprofile";
				m_Registry.m_RegisterMessage = "invalid xml message";

				// setup master user
				User masterUser = new User();
				masterUser.Login = "masterUser";
				masterUser.Password = "password1";
				masterUser.Email = "master@buzm.com";

				// add minimal hive configuration to master
				masterUser.SetHive( "<hive><guid>guid</guid><name>name</name></hive>" );

				// account creation with invalid defaults should succeed
				m_Result = m_Registry.InsertUser( masterUser, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Registration should pass despite invalid defaults: " + m_ResultMsg );

				// ensure no default hives or messages were added to the profile
				Assert.AreEqual( 1, masterUser.Hives.Count, "Incorrect hive count with bad default profile" );
				Assert.IsNotNull( masterUser.GetHive( "guid" ), "Configured hive missing after registration" );
				Assert.IsNull( masterUser.GetMessage( "register" ), "Unexpected info from bad default message" );

				// set valid registration defaults
				m_Registry.m_DefaultProfile = "masterUser";
				m_Registry.m_RegisterMessage = "<message><guid>register</guid><title>Welcome!</title></message>";

				// create new user account with valid defaults
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Account creation failed with valid defaults : " + m_ResultMsg );

				// ensure that the default hive was added to the profile
				Assert.AreEqual( 1, m_User.Hives.Count, "Expected single default hive after registration" );

				XmlNode hiveNode = m_User.GetHive(); // get first and only hive
				Assert.AreEqual( "name", hiveNode.SelectSingleNode( "name" ).InnerText, "Incorrect name for default hive" );
				Assert.AreNotEqual( "guid", hiveNode.SelectSingleNode( "guid" ).InnerText, "Guid unchanged for default hive" );

				// ensure that the default message was returned with the profile
				XmlNode messageNode = m_User.GetMessage( "register" ); // get registration message
				Assert.AreEqual( "Welcome!", messageNode.SelectSingleNode( "title" ).InnerText, "Incorrect title for register message" );

				// login user to see what was saved on disk
				m_Result = m_Registry.LoginUser( ref m_User, false, false, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Failed to login user: " + m_ResultMsg );

				// ensure that the register message was not saved with the profile
				Assert.IsNull( m_User.GetMessage( "register" ), "Unexpected message saved in disk profile" );

				// delete master profile so test can be repeated
				File.Delete( m_Registry.m_UsersFolder + masterUser.Login );
			}

			[Test] public void LoginUserTest()
			{				
				// set invalid login name
				m_User.Login = "buzmuser%";				
				
				// try to login user with invalid login name
				m_Result = m_Registry.LoginUser( ref m_User, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.UserError, "Tried to login with invalid name: " + m_ResultMsg );
				
				// make login valid
				m_User.Login = "buzmuser";

				// try to login non-existent user
				m_Result = m_Registry.LoginUser( ref m_User, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.UserError, "Tried to login non-existent user: " + m_ResultMsg );

				// register the current user 
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to register new user: " + m_ResultMsg );

				// new login attempt
				User newUser = new User();
				newUser.Login = m_User.Login;
				newUser.Password = "mypass2";

				// try logging in again with new user and wrong password
				m_Result = m_Registry.LoginUser( ref newUser, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.UserError, "Tried to login user with incorrect password: " + m_ResultMsg );

				// try logging in with new user and password checking disabled
				m_Result = m_Registry.LoginUser( ref newUser, false, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to login user with password check disabled: " + m_ResultMsg );
				
				// set correct password
				newUser.Password = m_User.Password;

				// try logging in again with correct password
				m_Result = m_Registry.LoginUser( ref newUser, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to login user with correct password: " + m_ResultMsg );
				Assert.AreEqual( m_User.Email, newUser.Email, "User email address was incorrect" );

				// try logging in with process hosts enabled but no hives in profile
				m_Result = m_Registry.LoginUser( ref newUser, true, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to login user with no hives: " + m_ResultMsg );
				Assert.AreEqual( 0, newUser.Hives.Count, "Unexpected hives returned for empty profile");

				// cleanup user profile for new cases
				File.Delete( m_Registry.m_UsersFolder + m_User.Login );
				newUser = m_User.CloneIdentity(); // reset newUser
				newUser.Email = m_User.Email; // required field

				// create a template that can be customized for multiple scenarios
				string hiveXmlTemplate = "<hive><name>hive{0}</name><guid>{0}</guid><host>{1}</host>"
					+ "<feeds><feed><guid>{2}</guid><url>url{2}</url></feed></feeds><members></members>"
					+ "<createDate>date{0}</createDate><skin><guid>skin{0}</guid></skin></hive>";      			

				// add 2 hives owned by user and 2 shared hives
				newUser.SetHive( String.Format( hiveXmlTemplate, "0", String.Empty, "a" ) );
				newUser.SetHive( String.Format( hiveXmlTemplate, "1", newUser.Login, "b" ) );
				newUser.SetHive( String.Format( hiveXmlTemplate, "2", "parentUser", "c" ) );
				newUser.SetHive( String.Format( hiveXmlTemplate, "3", "parentUser", "d" ) );

				// create profile for newUser on disk
				m_Result = m_Registry.InsertUser( newUser, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to register new user: " + m_ResultMsg );

				string newUserXml = newUser.ToXmlString(); // save newUser xml for comparison
				newUser = m_User.CloneIdentity(); // reset newUser profile for login
				
				// try to login with host parentUser profile missing on disk
				m_Result = m_Registry.LoginUser( ref newUser, true, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to login user with missing host: " + m_ResultMsg );
				Assert.AreEqual( newUserXml, newUser.ToXmlString(), "Profile mismatch after login with missing host");

				// create parent user
				User parentUser = new User();				
				parentUser.Login = "parentUser"; 
				parentUser.Password = "password2";
				parentUser.Email = "parent@buzm.com";

				// create empty profile for parent user on disk
				m_Result = m_Registry.InsertUser( parentUser, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to register parent user: " + m_ResultMsg );

				newUser = m_User.CloneIdentity();  // reset newUser profile
				
				// try to login with host parentUser lacking shared hives on disk
				m_Result = m_Registry.LoginUser( ref newUser, true, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to login user with host missing shared hives: " + m_ResultMsg );
				Assert.AreEqual( newUserXml, newUser.ToXmlString(), "Profile mismatch after login with host missing shared hives");

				// cleanup parent user profile for new cases
				File.Delete( m_Registry.m_UsersFolder + parentUser.Login );
				parentUser = parentUser.CloneIdentity(); // reset parent
				parentUser.Email = "okarim@buzm.com"; // required field

				// add shared hives to parent user
				parentUser.SetHive( String.Format( hiveXmlTemplate, "2", "", "p-c" ) );
				parentUser.SetHive( String.Format( hiveXmlTemplate, "3", "", "p-d" ) );

				// create profile with shared hives for parent user on disk
				m_Result = m_Registry.InsertUser( parentUser, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to register parent user: " + m_ResultMsg );

				string parentUserXml = parentUser.ToXmlString(); // save for comparison
				newUser = m_User.CloneIdentity();  // reset newUser profile
				
				// try to login with host parentUser having shared hives on disk
				m_Result = m_Registry.LoginUser( ref newUser, true, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to login user with host having shared hives: " + m_ResultMsg );
				Assert.IsFalse( newUserXml == newUser.ToXmlString(), "Profile match after login with properly setup host");

				// check to make sure both owned and hosted hives have correct config xml
				Assert.AreEqual( String.Format( hiveXmlTemplate, "0", String.Empty, "a" ), newUser.GetHive( "0" ).OuterXml, "Incorrect xml for owned hive 0");
				Assert.AreEqual( String.Format( hiveXmlTemplate, "1", "buzmuser", "b" ), newUser.GetHive( "1" ).OuterXml, "Incorrect xml for owned hive 1");
				Assert.AreEqual( String.Format( hiveXmlTemplate, "2", "parentUser", "p-c" ), newUser.GetHive( "2" ).OuterXml, "Incorrect xml for hosted hive 2");
				Assert.AreEqual( String.Format( hiveXmlTemplate, "3", "parentUser", "p-d" ), newUser.GetHive( "3" ).OuterXml, "Incorrect xml for hosted hive 3");

				// ensure that parent user can login as well
				m_Result = m_Registry.LoginUser( ref parentUser, true, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to login parent user: " + m_ResultMsg );
				Assert.AreEqual( parentUserXml, parentUser.ToXmlString(), "Profile mismatch after parent user login");

				// cleanup parent user profile from disk
				File.Delete( m_Registry.m_UsersFolder + parentUser.Login );
			}

			[Test] public void LoginUserLoadTest()
			{
				User childUser = m_User.CloneIdentity(); // clone user to avoid nested SetMember xml				
				childUser.Email = m_User.Email; // set email to mimick normal invite process
				childUser.Guid = "buzmuserguid"; // set guid for SetMember calls

				// iterate to create 25 parent users
				for( int x = 0; x < 25; x++ )
				{
					User parent = new User();
					parent.Guid = "ParentGuid" + x.ToString();
					parent.Login = "ParentLogin" + x.ToString();
					parent.Email = "parenttestuser@buzm.com";
					parent.Password = m_User.Password;	
					
					// iterate to add 10 hives to each user
					for( int y = 0; y < 10; y++ )
					{
						string hiveGuid = "Parent" + x.ToString() + "-" + y.ToString();
						string hiveXmlPrefix = "<hive><name>HiveName" + hiveGuid + "</name>"
							+ "<guid>" + hiveGuid + "</guid><feeds></feeds><members></members><host>";
						string hiveXmlSuffix = "</host><createDate>20051209180402</createDate>"
      						+ "<skin><guid>3633bcc2-faef-4ee0-bbee-e00f03fefeaf</guid></skin></hive>";

						parent.SetHive( hiveXmlPrefix + hiveXmlSuffix );
						
						if( y < 2 ) // set target child user as a member of 2 of every 10 hives
						{	
							// use clone for SetMember to avoid nested hive xml
							parent.SetMember( hiveGuid, childUser.ToXmlString() );
							m_User.SetHive( hiveXmlPrefix + parent.Login + hiveXmlSuffix  );
						}
						
						for( int z = 0; z < 50; z++ ) // iterate to add 50 feeds to each hive
						{
							parent.SetFeed( hiveGuid, "<feed><url>http://rss.news.yahoo.com/rss/us</url>"
								+ "<guid>" + hiveGuid + "-" + z.ToString() + "</guid><name>Yahoo News</name>"
								+ "<placement>1,2</placement></feed>" );
						}
					}
					
					m_Result = m_Registry.InsertUser( parent, out m_ResultMsg ); // create new test account on disk for each parent user
					Assert.IsTrue( m_Result == RegistryResult.Success, "Account creation failed for: " + parent.Login + " - " + m_ResultMsg );
				}

				// the target m_User should now have 50 hosted hives
				// iterate to add another 50 personal hives for a grand total of 100 hives
				for( int xx = 0; xx < 50; xx++ )
				{
					string hiveGuid = "m_User" + xx.ToString();
					m_User.SetHive( "<hive><name>HiveName" + hiveGuid + "</name>"
						+ "<guid>" + hiveGuid + "</guid><feeds></feeds><members></members>"
						+ "<host></host><createDate>20051209180402</createDate>"
						+ "<skin><guid>3633bcc2-faef-4ee0-bbee-e00f03fefeaf</guid></skin></hive>" );

					for( int zz = 0; zz < 50; zz++ ) // iterate to add 50 feeds to each hive
					{
						m_User.SetFeed( hiveGuid, "<feed><url>http://www.slashdot.org/rss/us</url>"
							+ "<guid>" + hiveGuid + "-" + zz.ToString() + "</guid><name>Slashdot</name>"
							+ "<placement>1,2</placement></feed>" );
					}
				}

				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg ); // create new test account on disk for target child user
				Assert.IsTrue( m_Result == RegistryResult.Success, "Account creation failed for: " + m_User.Login + " - " + m_ResultMsg );
				
				// record time it takes to login target user with large profile sans host hive configs
				User loginUser = m_User.CloneIdentity(); // clone target user to setup clean login
				DateTime startTime = DateTime.Now; // record time when login request began
				
				m_Result = m_Registry.LoginUser( ref loginUser, true, false, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Login failed for: " + loginUser.Login + " - " + m_ResultMsg );

				TimeSpan duration = DateTime.Now - startTime; // calculate login duration and write value to NUnit console
				Log.Write( TraceLevel.Info, "Large login (~500KB) sans hosts completed in: " + duration.ToString(), "Registry.LoginUserLoadTest" );

				// record time it takes to login user including host hive configs
				loginUser = m_User.CloneIdentity(); // clone target user to setup clean login
				startTime = DateTime.Now; // record time when login request began
				
				m_Result = m_Registry.LoginUser( ref loginUser, true, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Login failed for: " + loginUser.Login + " - " + m_ResultMsg );

				duration = DateTime.Now - startTime; // calculate login duration and write value to NUnit console
				Log.Write( TraceLevel.Info, "Large login (~860KB) with hosts completed in: " + duration.ToString(), "Registry.LoginUserLoadTest" );
				
				for( int i = 0; i < 25; i++ ) // cleanup parent user profiles
				{					
					string login = "ParentLogin" + i.ToString();					
					File.Delete( m_Registry.m_UsersFolder + login );
				}
			}

			[Test] public void InsertHiveTest()
			{				
				// create new user account for dummy user
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assertion.Assert( "Account creation failed: " + m_ResultMsg, m_Result == RegistryResult.Success );
				
				User user = m_User.CloneIdentity(); // copy registered login				
				user.Password = "invalidpassword"; // invalidate user password

				// try to add hive with invalid user
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.InsertHive, out m_ResultMsg );
				Assertion.Assert( "Tried to insert hive for invalid user: " + m_ResultMsg, m_Result == RegistryResult.UserError );

				// set valid password
				user.Password = "password1";

				// try to call InsertHive without any hive
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.InsertHive, out m_ResultMsg );
				Assertion.Assert( "Tried to insert hive without hive config: " + m_ResultMsg, m_Result == RegistryResult.ClientError );

				// add a hive and forcibly remove its guid value
				user.SetHive( "<hive><name>Hive0</name><guid>0</guid><feeds></feeds></hive>" );
				XmlNode hive = user.GetHive( "0" ); // get hive by guid
				XmlNode hiveGuid = hive.SelectSingleNode( "guid" );
				hive.RemoveChild( hiveGuid );

				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.InsertHive, out m_ResultMsg );
				Assertion.Assert( "Tried to insert hive without guid: " + m_ResultMsg, m_Result == RegistryResult.ClientError );

				// reset user to remove hive
				user = user.CloneIdentity();

				// add some valid hives to user - only the first one will be saved
				user.SetHive( "<hive><name>Hive0</name><guid>0</guid><feeds></feeds><members></members></hive>" );
				user.SetHive( "<hive><name>Hive1</name><guid>1</guid><feeds></feeds><members></members></hive>" );

				// add invalid member to the first hive
				User member = new User( );
				member.Guid = "one";
				member.Email = ",";
				user.SetMember( "0", member.ToXmlString() );

				// try to insert the hive with invalid member
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.InsertHive, out m_ResultMsg );
				Assertion.Assert( "Tried to insert hive with invalid member: " + m_ResultMsg, m_Result == RegistryResult.ServerError );

				// set valid email for member
				member.Email = "okarim@soaz.com";
				user.SetMember( "0", member.ToXmlString() );

				// try to insert the hive with valid member
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.InsertHive, out m_ResultMsg );
				Assertion.Assert( "Tried to insert valid hive member: " + m_ResultMsg, m_Result == RegistryResult.Success );

				// try to insert an existing hive
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.InsertHive, out m_ResultMsg );
				Assertion.Assert( "Tried to insert existing hive: " + m_ResultMsg, m_Result == RegistryResult.ClientError );
			}

			[Test] public void DeleteHiveAsHostTest()
			{				
				SetupMembershipTree(); // see method for the unique attributes of each user				
								
				User user = m_User.CloneIdentity(); // try to delete unknown hive from cloned user				
				user.SetHive( "<hive><name>HiveXX</name><guid>XX</guid><host></host></hive>" );

				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.DeleteHive, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Failed delete for missing Hive: " + m_ResultMsg );
				
				user = m_User.CloneIdentity(); // try to delete invalid hive from cloned user				
				user.SetHive( "<hive><name>HiveZZ</name><guid>&#39;</guid><host></host></hive>" );

				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.DeleteHive, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Failed delete for missing Hive: " + m_ResultMsg );

				// verify that global user has Hive1 in profile
				XmlNode hiveNode = GetHiveFromDisk( "buzmuser", "1" );
				Assert.IsNotNull( hiveNode, "Could not find Hive1 for buzmuzer before delete" );

				user = m_User.CloneIdentity(); // clone user and try to delete Hive1 
				user.SetHive( hiveNode.OuterXml ); // scenario: Hive1 has no host node and no members
				
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.DeleteHive, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Failed delete without host node: " + m_ResultMsg );
				
				// verify that Hive1 has been deleted from profile
				hiveNode = GetHiveFromDisk( "buzmuser", "1" );
				Assert.IsNull( hiveNode, "Found Hive1 for buzmuzer after delete" );

				// verify that correct users have Hive0
				hiveNode = GetHiveFromDisk( "buzmuser", "0" );
				Assert.IsNotNull( hiveNode, "Could not find Hive0 for buzmuzer before delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M1", "0" );
				Assert.IsNotNull( hiveNode, "Could not find Hive0 for L-H0-M1 user before delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M2", "0" );
				Assert.IsNotNull( hiveNode, "Could not find Hive0 for L-H0-M2 user before delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M1-M0", "0" );
				Assert.IsNotNull( hiveNode, "Could not find Hive0 for L-H0-M1-M0 user before delete" );

				user = m_User.CloneIdentity(); // clone user and try to delete Hive0 hierarchy
				user.SetHive( "<hive><guid>0</guid></hive>" ); // the delete should run two levels deep				
				
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.DeleteHive, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Failed to delete deep Hive0: " + m_ResultMsg );

				// verify that no users have Hive0
				hiveNode = GetHiveFromDisk( "buzmuser", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for buzmuzer after delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M0", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M0 user after delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M1", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M1 user after delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M2", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M2 user after delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M3", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M3 user after delete" );				
				hiveNode = GetHiveFromDisk( "L-H0-M1-M0", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M1-M0 user after delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M1-M1", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M1-M1 user after delete" );

				TeardownMembershipTree(); // delete all test member profiles
			}

			[Test] public void DeleteHiveAsMemberTest()
			{				
				SetupMembershipTree(); // see method for the unique attributes of each user
				
				User user = m_User.CloneIdentity(); // try to delete unknown hive from cloned user				
				user.SetHive( "<hive><name>HiveXX</name><guid>XX</guid><host></host></hive>" );

				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.DeleteHive, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Failed delete for missing Hive: " + m_ResultMsg );
				
				// scenario: delete hive where the configuration is valid and member has some children
				DeleteHiveFromDisk( "L-H0-M1", "0", "Failed valid delete where members has children" );	

				// check if hive was deleted from children as well
				XmlNode hiveNode = GetHiveFromDisk( "L-H0-M1-M0", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M1-M0 user after delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M1-M1", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M1-M1 user after delete" );

				// scenario: delete hive where the configuration is valid and member has no children
				DeleteHiveFromDisk( "L-H0-M2", "0", "Failed valid delete where members has no children" );	

				// scenario: delete hive which has no membership record in host profile
				DeleteHiveFromDisk( "L-H0-M5", "0", "Failed delete without membership record in host" );

				// scenario: delete hive with a host that the registry doesnt know about 
				DeleteHiveFromDisk( "L-H0-M6", "0", "Failed delete with unknown host" );				

				// scenario: delete hive which the host does not know about 
				DeleteHiveFromDisk( "L-H2-M0", "2", "Failed delete for hive that host does not know about" );				

				// scenario: delete hive where host is incorrectly set to same user
				DeleteHiveFromDisk( "L-H2-M1", "2", "Failed delete for hive where host is same user" );							
				
				TeardownMembershipTree(); // delete all test member profiles
			}

			[Test] public void DeleteHiveLoadTest()
			{						
				// setup a base hive for the root buzm user
				m_User.SetHive( "<hive><name>Hive0</name><guid>0</guid><host></host><members></members></hive>" );

				// iterate to add large number of members to hive
				for( int i = 0; i < LARGE_HIVE_MEMBERS; i++ )
				{
					// setup hive member
					User member = new User();
					member.Guid = "TestGuid" + i.ToString();
					member.Login = "TestLogin" + i.ToString();
					member.Email = "mytestuser@buzm.com";					
					member.Password = m_User.Password;	
					
					m_User.SetMember( "0", member.ToXmlString() ); // add member to root buzm user and then add hive to member
					member.SetHive( "<hive><name>Hive0</name><guid>0</guid><host>" + m_User.Login + "</host><members></members></hive>" );
					
					m_Result = m_Registry.InsertUser( member, out m_ResultMsg ); // create new user account for hive member
					Assert.IsTrue( m_Result == RegistryResult.Success, "Account creation failed for: " + member.Login + " - " + m_ResultMsg );
				}

				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg ); // create new account for root buzm user
				Assert.IsTrue( m_Result == RegistryResult.Success, "Root account creation failed: " + m_ResultMsg );

				User user = m_User.CloneIdentity(); // clone user and try to delete Hive0 with tons of members
				user.SetHive( "<hive><name>Hive0</name><guid>0</guid><host></host><members></members></hive>" );
				
				DateTime startTime = DateTime.Now; // record time when request began and then delete hive
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.DeleteHive, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Failed delete of large Hive: " + m_ResultMsg );
				
				TimeSpan duration = DateTime.Now - startTime; // calculate delete duration and write value to NUnit console
				Log.Write( TraceLevel.Info, "Large hive deleted in: " + duration.ToString(), "Registry.DeleteHiveLoadTest" );
								
				XmlNode hiveNode = GetHiveFromDisk( "buzmuser", "0" ); // verify Hive0 was deleted
				Assert.IsNull( hiveNode, "Found Hive0 for buzmuzer after delete" );

				// check if Hive0 was deleted for members and cleanup profiles
				for( int i = 0; i < LARGE_HIVE_MEMBERS; i++ )
				{					
					string login = "TestLogin" + i.ToString();					
					XmlNode memHiveNode = GetHiveFromDisk( login, "0" ); // verify delete
					Assert.IsNull( memHiveNode, "Found Hive0 for " + login + " after delete" );
					File.Delete( m_Registry.m_UsersFolder + login ); // cleanup member profile
				}
			}

			[Test] public void InsertMembersTest()
			{
				// try to insert members for non-existent user
				m_Result = m_Registry.AuthorizeUserAction( ref m_User, RegistryAction.InsertMembers, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.UserError, m_Result, "Tried to insert members for non-existent user: " + m_ResultMsg );

				string hiveXml = "<hive><name>Hive{0}</name><guid>{0}</guid><members /><inviteText>hello</inviteText></hive>";
				m_User.SetHive( String.Format( hiveXml, 0 ) ); // add a hive for test user

				// create new user account for test user
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Account creation failed: " + m_ResultMsg );

				User remoteUser = m_User.CloneIdentity(); // setup remote user to send invites from
				remoteUser.SetHive( String.Format( hiveXml, 1 ) ); // add non-existent hive to remote user

				// try to send invites for non-existent hive
				m_Result = m_Registry.AuthorizeUserAction( ref remoteUser, RegistryAction.InsertMembers, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.RegistryError, m_Result, "Tried to insert members for non-existent hive: " + m_ResultMsg );

				remoteUser = m_User.CloneIdentity(); // refresh remote user
				remoteUser.SetHive( String.Format( hiveXml, 0 ) ); // add existing hive

				// try to send invites with no members specified
				m_Result = m_Registry.AuthorizeUserAction( ref remoteUser, RegistryAction.InsertMembers, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Tried to send invite without any members: " + m_ResultMsg );

				// add some invalid members to the remote user
				UserConfigType memberOne = new UserConfigType();
				memberOne.Guid = "one";
				remoteUser.SetMember( "0", memberOne.ToXml() );

				UserConfigType memberTwo = new UserConfigType();
				memberTwo.Guid = "two";				
				remoteUser.SetMember( "0", memberTwo.ToXml() );

				// try to send invites with all members invalid
				m_Result = m_Registry.AuthorizeUserAction( ref remoteUser, RegistryAction.InsertMembers, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.ServerError, m_Result, "Tried to send invite with all invalid members: " + m_ResultMsg );

				// make one of the members valid
				memberOne.Email = "okarim@soaz.com";
				remoteUser.SetMember( "0", memberOne.ToXml() );

				// place read lock on registry file
				FileStream fs = File.OpenRead( m_Registry.m_UsersFolder + m_User.Login );

				// try to send invites with some members invalid and registry file locked
				m_Result = m_Registry.AuthorizeUserAction( ref remoteUser, RegistryAction.InsertMembers, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.RegistryError, m_Result, "Tried to send invite with registry file locked: " + m_ResultMsg );

				fs.Close(); // release lock and try to send invites with some members invalid again
				m_Result = m_Registry.AuthorizeUserAction( ref remoteUser, RegistryAction.InsertMembers, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Warning, m_Result, "Tried to send invite with some members invalid: " + m_ResultMsg );				

				// remove the invalid member
				remoteUser.RemoveMember( "0", memberTwo.Guid );

				// add another valid member
				UserConfigType memberThree = new UserConfigType();
				memberThree.Guid = "three";
				memberThree.Email = "omar@futureproofme.com";
				remoteUser.SetMember( "0", memberThree.ToXml() );

				// try to send invites with all members valid
				m_Result = m_Registry.AuthorizeUserAction( ref remoteUser, RegistryAction.InsertMembers, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Tried to send invite with all members valid: " + m_ResultMsg );

				// try to reload user from registry
				RegistryResult result = m_Registry.LoginUser( ref m_User, true, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Account login failed: " + m_ResultMsg );

				// check members to make sure the correct ones were saved
				Assert.AreEqual( 2, m_User.GetMembers( "0" ).Count, "Got incorrect count after invites completed" );
				Assert.IsNotNull( m_User.GetMember( "0", "one" ), "Could not find successfully invited member: one" );
				Assert.IsNotNull( m_User.GetMember( "0", "three" ), "Could not find successfully invited member: three" );
			}

			[Test] public void DeleteMembersTest()
			{				
				// see method for the unique attributes of each user
				SetupMembershipTree(); 

				User user = new User(); // clone test user
				user = m_User.CloneIdentity();
				
				// try to delete members from hive that doesn't exist
				user.SetHive( "<hive><name>HiveXX</name><guid>XX</guid><host></host></hive>" );
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.DeleteMembers, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.RegistryError, "Incorrect error for missing Hive: " + m_ResultMsg );								
								
				// try to send delete request with zero members - the members are populated from the memberGuids array passed to the method
				DeleteMembersFromDisk( "buzmuser", "0", new string[]{}, new string[]{}, false, RegistryResult.Success, "Delete request with zero members failed" );
				
				// try to send delete request for member that is not part of the hive according to the local registry
				DeleteMembersFromDisk( "buzmuser", "0", new string[]{ "G-H2-M0" }, new string[]{ "L-H2-M0" }, false, RegistryResult.Success, "Delete request with unknown member failed" );
								
				// try to send mixed request with one member missing from the registry, another that hasn't accepted the hive, and a third perfectly valid one
				DeleteMembersFromDisk( "buzmuser", "0", new string[]{ "G-H0-M4", "G-H0-M0", "G-H0-M1" }, new string[]{ "L-H0-M4", "L-H0-M0", "L-H0-M1" }, false, RegistryResult.RegistryError, "Multiple delete request with one unregistered member" );

				// try to send mixed request with one member that hasn't accepted the hive and two perfectly valid members
				DeleteMembersFromDisk( "buzmuser", "0", new string[]{ "G-H0-M0", "G-H0-M1", "G-H0-M2", "G-H0-M3" }, new string[]{ "L-H0-M0", "L-H0-M1", "L-H0-M2", "L-H0-M3" }, true, RegistryResult.Success, "Multiple delete request with valid members failed" );

				// check that Hive0 was deleted from children of L-H0-M1
				XmlNode hiveNode = GetHiveFromDisk( "L-H0-M1-M0", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M1-M0 user after delete" );
				hiveNode = GetHiveFromDisk( "L-H0-M1-M1", "0" );
				Assert.IsNull( hiveNode, "Found Hive0 for L-H0-M1-M1 user after delete" );

				TeardownMembershipTree(); // delete all test member profiles
			}
			
			[Test] public void InsertFeedsTest()
			{
				// add a hive for dummy user
				m_User.SetHive( "<hive><name>Hive0</name><guid>0</guid><feeds></feeds></hive>" );
			
				// create new user account for dummy user
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assertion.Assert( "Account creation failed: " + m_ResultMsg, m_Result == RegistryResult.Success );

				// add dummy feeds for the registered user				
				m_User.SetFeed( "0", "<feed><name>feed0</name><guid>0</guid></feed>" );
				m_User.SetFeed( "0", "<feed><name>feed1</name><guid>1</guid></feed>" );
				m_User.SetFeed( "0", "<feed><name>feed2</name><guid>2</guid></feed>" );
				
				// register valid new feeds for user
				m_Result = m_Registry.AuthorizeUserAction( ref m_User, RegistryAction.InsertFeeds, out m_ResultMsg );
				Assertion.Assert( "Tried to insert valid feeds for user: " + m_ResultMsg, m_Result == RegistryResult.Success );

				// invalidate username
				m_User.Login = "buzmuser2";
				m_Result = m_Registry.AuthorizeUserAction( ref m_User, RegistryAction.InsertFeeds, out m_ResultMsg );
				Assertion.Assert( "Tried to insert feeds with invalid user: " + m_ResultMsg, m_Result == RegistryResult.UserError );
				
				// create valid new user
				m_User.Login = "buzmuser";
				User newUser = new User();
				newUser = m_User.CloneIdentity();

				// add a new hive with feed to user that registry does not know about
				newUser.SetHive( "<hive><name>Hive2</name><guid>2</guid><feeds></feeds></hive>" );
				newUser.SetFeed( "2", "<feed><name>feed21</name><guid>21</guid></feed>" );
				
				// try to register feed with invalid hive
				m_Result = m_Registry.AuthorizeUserAction( ref newUser, RegistryAction.InsertFeeds, out m_ResultMsg );
				Assertion.Assert( "Tried to insert feed with invalid hive: " + m_ResultMsg, m_Result == RegistryResult.RegistryError );
			
				// add feed without guid
				newUser = new User();
				newUser = m_User.CloneIdentity();
				newUser.SetHive( "<hive><name>Hive0</name><guid>0</guid><feeds></feeds></hive>" );
				newUser.SetFeed( "0", "<feed><name>feed11</name><guid>11</guid></feed>" );
				XmlNode feed = newUser.GetFeed( "0", "11" );
				XmlNode feedGuid = feed.SelectSingleNode( "guid" );
				feed.RemoveChild( feedGuid );

				// try to register invalid feed
				m_Result = m_Registry.AuthorizeUserAction( ref newUser, RegistryAction.InsertFeeds, out m_ResultMsg );
				Assertion.Assert( "Tried to insert feed with no guid: " + m_ResultMsg, m_Result == RegistryResult.ClientError );
			}

			[Test] public void DeleteFeedsTest()
			{
				// add a hive and two feeds for dummy user
				m_User.SetHive( "<hive><name>Hive0</name><guid>0</guid><feeds></feeds></hive>" );
				m_User.SetFeed( "0", "<feed><name>feed0</name><guid>0</guid></feed>" );
				m_User.SetFeed( "0", "<feed><name>feed1</name><guid>1</guid></feed>" );

				// create new user account for dummy user
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Account creation failed: " + m_ResultMsg );

				// create valid new user
				User newUser = new User();
				newUser = m_User.CloneIdentity();
				
				// try to delete feed with hive that registry does not know about
				newUser.SetHive( "<hive><name>Hive1</name><guid>1</guid><feeds></feeds></hive>" );
				m_Result = m_Registry.AuthorizeUserAction( ref newUser, RegistryAction.DeleteFeeds, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.RegistryError, "Tried to remove feed with invalid hive: " + m_ResultMsg );
			
				// try to send empty delete message
				newUser = new User();
				newUser = m_User.CloneIdentity();
				newUser.SetHive( "<hive><name>Hive0</name><guid>0</guid><feeds></feeds></hive>" );
				m_Result = m_Registry.AuthorizeUserAction( ref newUser, RegistryAction.DeleteFeeds, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to send empty delete request: " + m_ResultMsg);

				// add one registered and one unregistered feed to delete request
				newUser.SetFeed( "0", "<feed><name>feed0</name><guid>0</guid></feed>" );
				newUser.SetFeed( "0", "<feed><name>feed1</name><guid>2</guid></feed>" );
				m_Result = m_Registry.AuthorizeUserAction( ref newUser, RegistryAction.DeleteFeeds, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to send valid delete request: " + m_ResultMsg);

				// try to remove feed without guid
				XmlNode feed = newUser.GetFeed( "0", "2" );
				XmlNode guidNode = feed.SelectSingleNode( "guid" );
				feed.RemoveChild( guidNode );
				m_Result = m_Registry.AuthorizeUserAction( ref newUser, RegistryAction.DeleteFeeds, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.ClientError, "Tried to delete feed without guid: " + m_ResultMsg);

				// load saved user profile
				User loginUser = new User();
				loginUser = m_User.CloneIdentity();
				m_Result = m_Registry.LoginUser( ref loginUser, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to login valid user: " + m_ResultMsg );

				// check expected user feed profile
				XmlNodeList feeds = loginUser.GetFeeds( "0" );
				Assert.AreEqual( 1, feeds.Count, "Incorrect feed count after delete" );
				string feedGuid = feeds[0].SelectSingleNode( "guid" ).InnerText;
				Assert.AreEqual( "1", feedGuid, "Incorrect feed left after delete" );
			}

			[Test] public void SaveUserTest()
			{				
				// try to save user that has not been authenticated
				m_Result = m_Registry.SaveUser( m_User, out m_ResultMsg );
				Assertion.Assert( "Tried to save unauthenticated user: " + m_ResultMsg, m_Result == RegistryResult.RegistryError );
				
				// create new user account for dummy user
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assertion.Assert( "Account creation failed: " + m_ResultMsg, m_Result == RegistryResult.Success );

				// try to login user
				m_Result = m_Registry.LoginUser( ref m_User, true, out m_ResultMsg );
				Assertion.Assert( "Tried to login user: " + m_ResultMsg, m_Result == RegistryResult.Success );

				// try to save user that has been authenticated
				m_Result = m_Registry.SaveUser( m_User, out m_ResultMsg );
				Assertion.Assert( "Tried to save authenticated user: " + m_ResultMsg, m_Result == RegistryResult.Success );				
			}

			[Test] public void SendGoodInvitesTest()
			{	
				// create a hive member
				User memberOne = new User( );
				memberOne.Guid = "one";
				memberOne.Email = "okarim@soaz.com";
				
				// create another hive member
				User memberTwo = new User( );
				memberTwo.Guid = "two";
				memberTwo.Email = "omkarim@yahoo.com";

				string hiveXml = "<hive><name>Hive0</name><guid>0</guid><members></members><inviteText></inviteText></hive>";
				XmlNode hive = m_User.SetHive( hiveXml ); // create a dummy hive
				
				// add members to the hive
				m_User.SetMember( "0", memberOne.ToXmlString() );
				m_User.SetMember( "0", memberTwo.ToXmlString() );

				// extract list of members
				XmlNodeList members = m_User.GetMembers( "0" );

				// send invites to the hive members
				m_Result = m_Registry.SendHiveInvites( m_User, members, hive, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Tried to send invites: " + m_ResultMsg );

				User localUser = m_User.CloneIdentity(); // create local user without new members
				localUser.Email = m_User.Email; // set email that will be used to send invites
				localUser.SetHive( hiveXml ); // add same hive to the local user

				// send invites and add invited members to the local user
				m_Result = m_Registry.SendHiveInvites( localUser, members, hive, true, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Tried to update local user with invites: " + m_ResultMsg );

				// ensure that successfully invited members were added to the local user
				Assert.IsNotNull( localUser.GetMember( "0", "one" ), "Could not find first successfully invited member" );
				Assert.IsNotNull( localUser.GetMember( "0", "two" ), "Could not find second successfully invited member" );
			}

			[Test] public void SendBadInvitesTest()
			{	
				// set invalid sending email
				m_User.Email = "";

				// create a hive member
				User memberOne = new User( );
				memberOne.Guid = "one";
				memberOne.Email = "omkarim@yahoo.com";
				
				// create another hive member
				User memberTwo = new User( );
				memberTwo.Guid = "two";
				memberTwo.Email = "okarim@soaz.com";

				string hiveXml = "<hive><name>Hive0</name><guid>0</guid><members></members><inviteText>Hey there, this is a Registry test</inviteText></hive>";
				XmlNode hive = m_User.SetHive( hiveXml ); // create a dummy hive
				
				// add members to the hive
				m_User.SetMember( "0", memberOne.ToXmlString() );
				m_User.SetMember( "0", memberTwo.ToXmlString() );

				// extract list of members
				XmlNodeList members = m_User.GetMembers( "0" );

				// send invite with invalid sender email to the hive members
				m_Result = m_Registry.SendHiveInvites( m_User, members, hive, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.ServerError, m_Result, "Tried to send invite with invalid From email: " + m_ResultMsg );
			
				// set valid sending email
				m_User.Email = "okarim@buzm.com";

				// invalidate member one
				memberOne.Email = ",";
				m_User.SetMember( "0", memberOne.ToXmlString() );

				// create another member
				User memberThree = new User();
				memberThree.Guid = "three";
				memberThree.Email = "omar@futureproofme.com";

				// add new member and remove its guid node
				XmlNode member = m_User.SetMember( "0", memberThree.ToXmlString() );				
				XmlNode guidNode = member.SelectSingleNode( "guid" );
				member.RemoveChild( guidNode );

				// re-extract list of members
				members = m_User.GetMembers( "0" );

				// create new user to send invites from
				User fromUser = m_User.CloneIdentity(); 
				fromUser.Email = m_User.Email; 
				fromUser.SetHive( hiveXml ); 

				// send invites and try to update fromUser with 2 invalid members out of 3 total
				m_Result = m_Registry.SendHiveInvites( fromUser, members, hive, true, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Warning, m_Result, "Tried to send invite with 2 invalid members out of 3 total: " + m_ResultMsg );

				// the resultMsg should contain email addresses for failed invites only
				Assert.IsTrue( m_ResultMsg.Contains( memberOne.Email ), "memberOne email was not found in failure message" );
				Assert.IsTrue( m_ResultMsg.Contains( memberThree.Email ), "memberThree email was not found in failure message" );
				Assert.IsFalse( m_ResultMsg.Contains( memberTwo.Email ), "memberTwo email was unexpectedly found in failure message" );

				// only the one valid member should have been added to the fromUser
				Assert.AreEqual( 1, fromUser.GetMembers( "0" ).Count, "Got incorrect member count for fromUser" );
				Assert.IsNotNull( fromUser.GetMember( "0", "two" ), "Incorrect member was added to fromUser" );
			}

			[Test] public void AcceptInviteTest ( )
			{					
				// register new user that will be invited to Hive - hereafter referred to as "invitee" :) 
				m_Result = m_Registry.ProcessUserAction( ref m_User, RegistryAction.InsertUser, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to register new invitee user: " + m_ResultMsg );

				// try to accept invite from non-existent host
				m_User.SetHive( "<hive><name>Hive0</name><guid>1</guid><host>hostUser</host><members></members><skin><guid></guid></skin></hive>" ); 
				m_Result = m_Registry.ProcessUserAction( ref m_User, RegistryAction.AcceptInvite, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.UserError, "Tried to accept invititation from non-existent host: " + m_ResultMsg );

				// create host user
				User hostUser = new User();
				hostUser.Login = "hostUser";
				hostUser.Password = "hostPass1";
				hostUser.Email = "hostUser@buzm.com";

				// set new hive for hostUser and add invitee member to it
				string hiveXml = "<hive><name>Hive0</name><guid>0</guid><host>hostUser</host><members></members></hive>";
				hostUser.SetHive( hiveXml ); // add hive to the host user profile
				m_User.Guid = "One"; // set guid for invitee so SetMember will work 
				hostUser.SetMember( "0", m_User.ToXmlString() );

				// register user that will send out the Hive invitation - referred to as "host"
				m_Result = m_Registry.ProcessUserAction( ref hostUser, RegistryAction.InsertUser, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to register host user: " + m_ResultMsg );				

				// try to accept invite for non-existent Hive
				m_Result = m_Registry.ProcessUserAction( ref m_User, RegistryAction.AcceptInvite, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.UserError, "Tried to accept invititation for non-existent Hive: " + m_ResultMsg );

				// reset invitee user object
				m_User = m_User.CloneIdentity();
				m_User.Guid = "Two"; // set invitee guid to mismatch against existing host Hive
				m_User.SetHive( "<hive><name>Hive0</name><guid>0</guid><host>hostUser</host><members></members><createDate></createDate><skin><guid></guid></skin></hive>" ); 
				m_Result = m_Registry.ProcessUserAction( ref m_User, RegistryAction.AcceptInvite, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.UserError, "Tried to accept invitation with mismatched member guid: " + m_ResultMsg );

				m_User.Guid = "One"; // set correct invitee guid - now hive createDate and skin guid are missing
				m_Result = m_Registry.ProcessUserAction( ref m_User, RegistryAction.AcceptInvite, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.RegistryError, "Tried to accept invitation with missing hive settings: " + m_ResultMsg );

				// delete host profile so we can create a new one
				File.Delete( m_Registry.m_UsersFolder + hostUser.Login );

				// setup host user one more time
				hostUser = hostUser.CloneIdentity(); 
				hostUser.Email = "hostUser@buzm.com";
				
				// this time add feed, createDate and skin data to Hive xml
				hiveXml = "<hive><name>Hive0</name><guid>0</guid><host></host>"
					+ "<feeds><feed><guid>Feed0</guid></feed></feeds><members></members>"
					+ "<createDate>20041010101010</createDate><skin><guid>1</guid></skin></hive>";
				
				hostUser.SetHive( hiveXml ); // add Hive to the host profile
				m_User.Login = ""; // clear invitee login to mimick emailed invite
				hostUser.SetMember( "0", m_User.ToXmlString() );

				// register host user once again
				m_Result = m_Registry.ProcessUserAction( ref hostUser, RegistryAction.InsertUser, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to register host user again: " + m_ResultMsg );

				// try to complete a successful Hive invitation
				m_User.Login = "buzmuser"; // reset invitee login so registry can locate profile
				m_Result = m_Registry.ProcessUserAction( ref m_User, RegistryAction.AcceptInvite, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to accept valid invitation: " + m_ResultMsg );
				
				// verify skin guid property
                XmlNode hiveNode = m_User.GetHive( "0" );
				SafeXmlDoc hiveDoc = new SafeXmlDoc( hiveNode.OuterXml );
				string skinGuid = hiveDoc.GetInnerText( "/hive/skin/guid", "Test" );
				Assert.AreEqual( "1", skinGuid, "Got invalid skin after Hive registration." );

				// verify createDate property
				string createDate = hiveDoc.GetInnerText( "/hive/createDate", "Test" );
				Assert.AreEqual( "20041010101010", createDate, "Got invalid createDate after Hive registration." );

				// verify hive host property
				string hiveHost = hiveDoc.GetInnerText( "/hive/host", "Test" );
				Assert.AreEqual( "hostUser", hiveHost, "Got invalid host after Hive registration." );
				
				// verify shared feed node exists
				XmlNode sharedFeed = m_User.GetFeed( "0", "Feed0" );
				Assert.IsNotNull( sharedFeed, "Did not get shared feed from host profile." );

				// try to accept invitation for the same Hive again
				m_Result = m_Registry.ProcessUserAction( ref m_User, RegistryAction.AcceptInvite, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.UserError, "Tried to accept valid invitation again: " + m_ResultMsg );

				// create another user
				User otherUser = new User();
				otherUser.Login = "otherUser";
				otherUser.Password = "otherPass1";
				otherUser.Email = "otherUser@buzm.com";				

				// register another user profile
				m_Result = m_Registry.ProcessUserAction( ref otherUser, RegistryAction.InsertUser, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Tried to register another user: " + m_ResultMsg );

				otherUser.Guid = "One"; // set same guid and hive as m_User
				otherUser.SetHive( "<hive><name>Hive0</name><guid>0</guid><host>hostUser</host><members></members><createDate></createDate><skin><guid></guid></skin></hive>" ); 

				// try to accept invitation as if it has been email forwarded by m_User
				m_Result = m_Registry.ProcessUserAction( ref otherUser, RegistryAction.AcceptInvite, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.UserError, "Tried to accept forwarded hive invitation: " + m_ResultMsg );

				// delete user profiles so the test can be run again
				File.Delete( m_Registry.m_UsersFolder + hostUser.Login );
				File.Delete( m_Registry.m_UsersFolder + otherUser.Login );
			}

			[Test] public void ProcessUserTest()
			{
				// try to register user
				m_Result = m_Registry.ProcessUserAction( ref m_User, RegistryAction.InsertUser, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Tried to register new user: " + m_ResultMsg );

				// try to add hive to user
				m_User.SetHive( "<hive><name>Hive0</name><guid>0</guid><feeds /><members /></hive>" );
				m_Result = m_Registry.ProcessUserAction( ref m_User, RegistryAction.InsertHive, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Tried to add hive for user: " + m_ResultMsg );

				// try to execute invalid action against user
				m_Result = m_Registry.ProcessUserAction( ref m_User, (RegistryAction)10000, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.UnknownRequest, m_Result, "Tried to make invalid process request: " + m_ResultMsg );

				// create another user
				User newUser = new User();
				newUser.Login = m_User.Login;
				newUser.Password = m_User.Password;

				// try to login user
				m_Result = m_Registry.ProcessUserAction( ref newUser, RegistryAction.LoginUser, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Tried to login new user: " + m_ResultMsg );
				Assert.AreEqual( m_User.Email, newUser.Email, "User email address was incorrect" );
				
				// try to add a feed for user
				newUser.SetFeed( "0", "<feed><name>feed00</name><guid>00</guid></feed>" );
				m_Result = m_Registry.ProcessUserAction( ref newUser, RegistryAction.InsertFeeds, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Tried to add feed for user: " + m_ResultMsg );

				// try to add a member to hive
				newUser.SetMember( "0", "<user><guid>one</guid><email>okarim@soaz.com</email><login /></user>" );
				m_Result = m_Registry.ProcessUserAction( ref newUser, RegistryAction.InsertMembers, out m_ResultMsg );
				Assert.AreEqual( RegistryResult.Success, m_Result, "Tried to add member to hive: " + m_ResultMsg );
			}

			private void SetupMembershipTree( )
			{				
				// setup a few hives for the global user
				m_User.SetHive( "<hive><name>Hive0</name><guid>0</guid><host></host><members></members></hive>" );
				m_User.SetHive( "<hive><name>Hive1</name><guid>1</guid><feeds></feeds><members></members></hive>" );				

				// create some level one hive members
				// scenario: profile exists but has not accepted hive
				User member0 = new User( ); 
				member0.Guid = "G-H0-M0"; // hive 0 - member 0
				member0.Email = "okarim@soaz.com";
				member0.Password = m_User.Password;
				
				// scenario: profile exists and has accepted hive and invited others as well
				User member1 = new User( ); 
				member1.Guid = "G-H0-M1"; // hive 0 - member 1
				member1.Email = "omkarim@yahoo.com";
				member1.Login = "L-H0-M1"; // invite accepted
				member1.Password = m_User.Password;
				
				// scenario: profile exists and has accepted hive
				User member2 = new User( );
				member2.Guid = "G-H0-M2"; // hive 0 - member 2
				member2.Email = "omkarim@buzm.com";
				member2.Login = "L-H0-M2"; // invite accepted
				member2.Password = m_User.Password;
				
				// scenario: profile exists and has accepted and then deleted hive
				User member3 = new User( );
				member3.Guid = "G-H0-M3"; // hive 0 - member 3
				member3.Email = "omkarim@buzm.com";
				member3.Login = "L-H0-M3"; // invite accepted
				member3.Password = m_User.Password;

				// scenario: profile does not exist but seems to have accepted hive
				User member4 = new User( );
				member4.Guid = "G-H0-M4"; // hive 0 - member 4
				member4.Email = "omkarim@buzm.com";
				member4.Login = "L-H0-M4"; // invite accepted
				member4.Password = m_User.Password;

				// scenario: member has hive but host doesn't know about membership
				User member5 = new User( );
				member5.Guid = "G-H0-M5"; // hive 0 - member 5
				member5.Email = "omkarim@buzm.com";
				member5.Login = "L-H0-M5"; // invite accepted
				member5.Password = m_User.Password;		

				// scenario: member has hive but contains the incorrect host login 
				User member6 = new User( );
				member6.Guid = "G-H0-M6"; // hive 0 - member 6
				member6.Email = "omkarim@buzm.com";
				member6.Login = "L-H0-M6"; // invite accepted
				member6.Password = m_User.Password;		

				// add appropriate members to the hive
				m_User.SetMember( "0", member0.ToXmlString() );
				m_User.SetMember( "0", member1.ToXmlString() );
				m_User.SetMember( "0", member2.ToXmlString() );
				m_User.SetMember( "0", member3.ToXmlString() );
				m_User.SetMember( "0", member4.ToXmlString() );

				// create new user account for root user
				m_Result = m_Registry.InsertUser( m_User, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Root account creation failed: " + m_ResultMsg );

				// add Hive0 to level one user accounts - Member0 has not accepted invite as yet and Member3 already deleted Hive
				member1.SetHive( "<hive><name>Hive0</name><guid>0</guid><host>" + m_User.Login + "</host><members></members></hive>" );
				member2.SetHive( "<hive><name>Hive0</name><guid>0</guid><host>" + m_User.Login + "</host><members></members></hive>" );
				member5.SetHive( "<hive><name>Hive0</name><guid>0</guid><host>" + m_User.Login + "</host><members></members></hive>" );
				member6.SetHive( "<hive><name>Hive0</name><guid>0</guid><host>unknownuser</host><members></members></hive>" );

				// create some level two hive members
				User member10 = new User( );
				member10.Guid = "G-H0-M1-M0"; // hive 0 - member 1 -> member 0
				member10.Email = "okarim@soaz.com";
				member10.Login = "L-H0-M1-M0"; // invite accepted
				member10.Password = m_User.Password;

				User member11 = new User( );
				member11.Guid = "G-H0-M1-M1"; // hive 0 - member 1 -> member 1
				member11.Email = "omkarim@yahoo.com";
				member11.Password = m_User.Password;

				// add level two members to hive 0 - member 1
				member1.SetMember( "0", member10.ToXmlString() );
				member1.SetMember( "0", member11.ToXmlString() );

				// set member0 login so registration will work
				member0.Login = "L-H0-M0"; // hive 0 - member 0

				// create new user accounts for level one members
				m_Result = m_Registry.InsertUser( member0, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member0 account creation failed: " + m_ResultMsg );
				m_Result = m_Registry.InsertUser( member1, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member1 account creation failed: " + m_ResultMsg );
				m_Result = m_Registry.InsertUser( member2, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member2 account creation failed: " + m_ResultMsg );
				m_Result = m_Registry.InsertUser( member3, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member3 account creation failed: " + m_ResultMsg );	
				m_Result = m_Registry.InsertUser( member5, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member5 account creation failed: " + m_ResultMsg );	
				m_Result = m_Registry.InsertUser( member6, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member6 account creation failed: " + m_ResultMsg );	

				// add Hive0 to level two user accounts (invited by Member1) - Member11 has not accepted invite as yet
				member10.SetHive( "<hive><name>Hive0</name><guid>0</guid><host>" + member1.Login + "</host><members></members></hive>" );

				// set Member11 login so registration will work
				member11.Login= "L-H0-M1-M1"; // hive 0 - member 1 -> member 1

				// create new user accounts for Hive0 level two members
				m_Result = m_Registry.InsertUser( member10, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member10 account creation failed: " + m_ResultMsg );
				m_Result = m_Registry.InsertUser( member11, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member11 account creation failed: " + m_ResultMsg );

				// scenario: member has Hive2 which the host doesn't seem to know about
				User member7 = new User( );
				member7.Guid = "G-H2-M0"; // hive 2 - member 0
				member7.Email = "omkarim@buzm.com";
				member7.Login = "L-H2-M0";
				member7.Password = m_User.Password;

				// scenario: member has Hive2 with the host set to themselves
				User member8 = new User( );
				member8.Guid = "G-H2-M1"; // hive 2 - member 1
				member8.Email = "omkarim@buzm.com";
				member8.Login = "L-H2-M1";
				member8.Password = m_User.Password;

				member7.SetHive( "<hive><name>Hive2</name><guid>2</guid><host>" + m_User.Login + "</host><members></members></hive>" );
				member8.SetHive( "<hive><name>Hive2</name><guid>2</guid><host>L-H2-M1</host><members></members></hive>" );
				
				m_Result = m_Registry.InsertUser( member7, out m_ResultMsg ); // register member7 with non-existent hive
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member7 account creation failed: " + m_ResultMsg );	
				m_Result = m_Registry.InsertUser( member8, out m_ResultMsg ); // register member8 with host set incorrectly
				Assert.IsTrue( m_Result == RegistryResult.Success, "Member8 account creation failed: " + m_ResultMsg );	
			}

			private void TeardownMembershipTree( )
			{
				File.Delete( m_Registry.m_UsersFolder + "L-H0-M0" );
				File.Delete( m_Registry.m_UsersFolder + "L-H0-M1" );
				File.Delete( m_Registry.m_UsersFolder + "L-H0-M2" );
				File.Delete( m_Registry.m_UsersFolder + "L-H0-M3" );
				File.Delete( m_Registry.m_UsersFolder + "L-H0-M5" );
				File.Delete( m_Registry.m_UsersFolder + "L-H0-M6" );
				File.Delete( m_Registry.m_UsersFolder + "L-H0-M1-M0" );
				File.Delete( m_Registry.m_UsersFolder + "L-H0-M1-M1" );
				File.Delete( m_Registry.m_UsersFolder + "L-H2-M0" );
				File.Delete( m_Registry.m_UsersFolder + "L-H2-M1" );
			}

			private XmlNode GetHiveFromDisk( string login, string hiveGuid )
			{
				User user = m_User.CloneIdentity(); // clone global user
				user.Login = login; // modify login but use global password

				m_Result = m_Registry.LoginUser( ref user, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Login failed: " + user.Login + " - " + m_ResultMsg );
				return user.GetHive( hiveGuid ); // return hive node or null if no hive was found
			}

			private void DeleteHiveFromDisk( string login, string hiveGuid, string failureMessage )
			{
				XmlNode hiveNode = GetHiveFromDisk( login, hiveGuid ); // check if hive exists on disk
				Assert.IsNotNull( hiveNode, "Could not find Hive" + hiveGuid + " for user " + login + " before delete" );
	
				User user = m_User.CloneIdentity();	
				user.Login = login; // password is same as m_User
				user.SetHive( hiveNode.OuterXml ); // set hive for request
	
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.DeleteHive, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, login + " - " + failureMessage + " - " + m_ResultMsg );

				hiveNode = GetHiveFromDisk( login, hiveGuid ); // ensure hive is no longer on disk
				Assert.IsNull( hiveNode, "Found Hive" + hiveGuid + " for user " + login + " after delete" );
			}

			private XmlNode GetMemberFromDisk( string login, string hiveGuid, string memberGuid )
			{
				User user = m_User.CloneIdentity(); // clone global user
				user.Login = login; // modify login but use global password

				m_Result = m_Registry.LoginUser( ref user, true, out m_ResultMsg );
				Assert.IsTrue( m_Result == RegistryResult.Success, "Login failed: " + user.Login + " - " + m_ResultMsg );
				return user.GetMember( hiveGuid, memberGuid ); // return member node or null if no member was found
			}

			private void DeleteMembersFromDisk( string login, string hiveGuid, string[] memberGuids, string[] memberLogins, 
												bool checkMemExists, RegistryResult expectedResult, string failureMessage )
			{	
				XmlNode hiveNode = GetHiveFromDisk( login, hiveGuid ); // check if hive exists on disk
				Assert.IsNotNull( hiveNode, "Could not find Hive" + hiveGuid + " for user " + login + " before member delete" );
	
				User user = m_User.CloneIdentity();	// clone user to get password
				user.Login = login; // set login and hive as specified for the request
				user.SetHive( "<hive><guid>" + hiveGuid.ToString() + "</guid><host></host><members></members></hive>" ); 
			
				for( int x=0; x < memberGuids.Length; x++ ) // iterate and add members to request
				{
					// setup member object
					User member = new User();
					member.Guid = memberGuids[x];
					member.Login = memberLogins[x];								
					member.Password = m_User.Password;	

					// add member to the delete request
					user.SetMember( hiveGuid, member.ToXmlString() ); 
					if( checkMemExists ) // check if membership exists on disk
					{
						XmlNode memNode = GetMemberFromDisk( login, hiveGuid, memberGuids[x] ); 
						Assert.IsNotNull( memNode, "Could not find Member " + memberGuids[x] + " for user " + login + " before delete" );
					}
				}
				
				// delete members from host profile - should delete specified hive from member profiles as well
				m_Result = m_Registry.AuthorizeUserAction( ref user, RegistryAction.DeleteMembers, out m_ResultMsg );
				Assert.IsTrue( m_Result == expectedResult, login + " - " + failureMessage + " - " + m_ResultMsg );

				if( checkMemExists ) // check if membership still exist in host profile
				{
					for( int y=0; y < memberGuids.Length; y++ ) // iterate requested members
					{					
						XmlNode memNode = GetMemberFromDisk( login, hiveGuid, memberGuids[y] ); // check if membership exists
						Assert.IsNull( memNode, "Found Member " + memberGuids[y] + " for user " + login + " after member delete" );

						XmlNode memHiveNode = GetHiveFromDisk( memberLogins[y], hiveGuid ); // check if hive exists in member profile
						Assert.IsNull( memHiveNode, "Found Hive " + hiveGuid + " for user " + memberLogins[y] + " after member delete" );					 										
					}
				}
			}
		}

		#endif
		#endregion

	}
}
