using System;
using System.IO;
using System.Xml;
using System.Web.Mail;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Buzm.Utility;

namespace Buzm.Register
{
	/// <summary>Provides basic member
	/// invitation support via email</summary>
	public class UserInvite : MailMessage
	{
		private User m_FromUser;
		private string m_HiveGuid;	
		private string m_HiveName;
		private string m_InviteXml;
		private string m_Attachment;

		// match strings used to replace variables in text
		private const string HIVE_NAME_MATCH = "[HIVE_NAME]";

		public UserInvite( User fromUser, string hiveName, string hiveGuid, string message )
		{
			m_FromUser = fromUser;
			m_HiveGuid = hiveGuid;
			m_HiveName = hiveName;
			base.From = fromUser.Email;
			
			// read default parameters from configuration file
			string footer = Config.GetValue( "preferences/invite/footer" );
			string subject = Config.GetValue( "preferences/invite/subject" );
			m_Attachment = Config.GetValue( "preferences/invite/attachment" );
			m_InviteXml = Config.GetOuterXml( "templates/config/invite" );

			// personalize default email configuration parameters
			base.Subject = subject.Replace( HIVE_NAME_MATCH, m_HiveName );
			base.Body = message + footer.Replace( HIVE_NAME_MATCH, m_HiveName );

			// set outgoing SMTP server - TODO: authentication config
			SmtpMail.SmtpServer = Config.GetValue( "network/smtpServer" );
		}

		public bool Send( User toUser )
		{
			try // sending email to each invited user
			{	
				// create unique temporary path for attachment
				string tempFolder = FileUtils.CreateTempFolder();
				string attachFile = tempFolder + m_Attachment;
				
				// create xml document to format invite attachment
				SafeXmlDoc inviteDoc = new SafeXmlDoc( m_InviteXml );
				inviteDoc.SetInnerText( "/invite/guid", toUser.Guid, "UserInvite.Send" );
				inviteDoc.SetInnerText( "/invite/hive/guid", m_HiveGuid, "UserInvite.Send" );
				inviteDoc.SetInnerText( "/invite/hive/name", m_HiveName, "UserInvite.Send" );
				inviteDoc.SetInnerText( "/invite/hive/host", m_FromUser.Login, "UserInvite.Send" );
				
				// create temp file and attach it to the message
				inviteDoc.SaveToFile( attachFile, "UserInvite.Send" ); 				
				MailAttachment attach = new MailAttachment( attachFile );
				base.Attachments.Clear(); // remove previous attachments
				base.Attachments.Add( attach ); // add invite to message

				base.To = toUser.Email; // set the outgoing email address
				SmtpMail.Send( this ); // send the email asynchronously
				Directory.Delete( tempFolder, true ); // cleanup files
				return true; // if no exceptions thrown in processing
			}
			catch( Exception e )
			{
				// TODO: It seems CDO doesn't release the lock on the 
				// attachment if an exception is thrown so the temp folder
				// and file will not be cleaned up if the smtp process fails
				// If lock is released Directory.Delete must be in finally block 

				Log.Write( "Could not send invite email to: " + toUser.Email,
				TraceLevel.Warning, "UserInvite.Send", e );
				return false; // since the send failed
			}
		}
		
		#region NUnit Automated Test Cases

		[TestFixture] public class UserInviteTest
		{
			private User m_HostUser;
			private UserInvite m_Invite;

			[SetUp] public void SetUp() 
			{ 
				// Load local config file for this assembly
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly );

				// create host user
				m_HostUser = new User( );
				m_HostUser.Login = "omar_karim";
				m_HostUser.Email = "okarim@buzm.com";

				// create a user invite from the host user
				string message = "Hey, check out the cool hive I just created.";
				m_Invite = new UserInvite( m_HostUser, "Sports", "0", message );									
			}

			[TearDown] public void TearDown() { }
			
			[Test] public void SendGoodInvitesTest() 
			{ 				
				// create a hive member
				User member = new User( );
				member.Guid = "one";
				member.Email = "okarim@soaz.com";
				
				bool success = m_Invite.Send( member ); // send hive invite to prospective member
				Assertion.Assert( "Could not send invite email: " + member.Email, success );

				// create another hive member
				member = new User( );
				member.Guid = "two";
				member.Email = "omkarim@yahoo.com";
				
				success = m_Invite.Send( member ); // send hive invite to prospective member
				Assertion.Assert( "Could not send invite email: " + member.Email, success );
			}

			[Ignore( "Does not cleanup temp folders." )] 
			[Test] public void SendBadInvitesTest() 
			{ 	
				// create another hive member with invalid email
				User member = new User( );
				member.Guid = "one";
				member.Email = ",";
				
				bool success = m_Invite.Send( member ); // send hive invite to prospective member
				Assertion.Assert( "Sent email with invalid address: " + member.Email, !success );

				// create another hive member with valid email
				member = new User( );
				member.Guid = "three";
				member.Email = "omkarim@yahoo.com";
			
				// set smtp server to invalid value
				SmtpMail.SmtpServer = "nosuchserver";
				success = m_Invite.Send( member ); // send hive invite with invalid server
				Assertion.Assert( "Send should not work with invalid SMTP server", !success );
			}
		}

		#endregion
	}
}
