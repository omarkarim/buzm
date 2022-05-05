using System;
using Buzm.Utility;
using Buzm.Schemas;
using Buzm.Register;
using NUnit.Framework;
using System.Collections;
using System.Windows.Forms;

namespace Buzm.Hives
{
	public partial class MemberEditor : RegistryEditor
	{
		private User m_HiveUser;
		private ArrayList m_Members;
		private HiveModel m_SelectedHive;
		private HiveManager m_HiveManager;

		private readonly char[] DELIMITERS = new char[]{ ',' , ';' };

		public MemberEditor( ){ } // empty constructor for NUnit framework
		public MemberEditor( User user, HiveManager hiveManager, HiveModel[] userHives )
		{
			m_HiveUser = user;			
			m_HiveManager = hiveManager;
			Action = RegistryAction.InsertMembers;

			InitializeComponent(); // designer code
			m_HiveComboBox.Items.AddRange( userHives );

			ActiveControl = m_InviteEmailTextBox; // set input focus
			m_InviteMessageTextBox.Text = Config.GetValue( "preferences/invite/message" );			
		}

		private void m_AcceptButton_Click( object sender, EventArgs e )
		{
			// if a hive has been selected by user
			if( m_HiveComboBox.SelectedItem != null )
			{
				m_SelectedHive = (HiveModel)m_HiveComboBox.SelectedItem;
				string hiveGuid = m_SelectedHive.Guid; // extract guid
				
				// if selected hive still exists in active hive table				
				if( m_HiveManager.HiveModels.Contains( hiveGuid ) )
				{
					string[] inviteEmails; // extract valid emails from textbox
					if( ParseEmailList( m_InviteEmailTextBox.Text, out inviteEmails ) )
					{
						DisableInterface(); // disable UI to prevent resubmit
						m_SelectedHive.InviteText = m_InviteMessageTextBox.Text;

						// set registry action with user info and selected hive
						ActionUser = m_HiveUser.CloneIdentity(); // clone user
						ActionUser.SetHive( m_SelectedHive.ConfigToXml() );

						m_Members = new ArrayList(); // add members
						foreach( string inviteEmail in inviteEmails )
						{
							UserConfigType member = new UserConfigType();							
							member.Login = String.Empty; // placeholder

							member.Email = inviteEmail; // parsed email							
							member.Guid = Guid.NewGuid().ToString();
														
							ActionUser.SetMember( hiveGuid, member.ToXml() );
							m_Members.Add( member ); // save for response
						}

						// submit new invites to the registry for processing
						BeginRegistryRequest(); // begin asynchronous update
					}
					else // valid email addresses could not be parsed from user input
					{
						if( inviteEmails.Length == 0 ) AlertUser( "Please enter at least one email address." );
						else AlertUser( "Please correct the following email address: " + inviteEmails[0] );
					}
				}
				else AlertUser( "The selected Hive no longer exists in your profile." );
			}
			else AlertUser( "Please select a Hive for this invite." );
		}

		public override void RegistryEditor_RegistryResponse( object sender, RegistryEventArgs e )
		{
			if( EndRegistryRequest( e ) ) // if registry request complete
			{
				if( ( e.Result == RegistryResult.Success ) || // all sent
					( e.Result == RegistryResult.Warning ) ) // some sent
				{
					// ensure members and hive still setup after callback
					if( ( m_Members != null ) && ( m_SelectedHive != null ) )
					{
						// also check if the hive still exists in the active table
						if( m_HiveManager.HiveModels.Contains( m_SelectedHive.Guid ) )	 
						{
							string failedEmails = String.Empty; // parse members
							foreach( UserConfigType member in m_Members )
							{
								// result message will contain failed emails
								if( ( e.Result == RegistryResult.Warning ) && 
									( e.ResultMessage.Contains( member.Email ) ) )
								{
									// build string of failed email addresses
									failedEmails += member.Email + DELIMITERS[0];
								}
								else m_SelectedHive.AddMember( member, this );
							}
							m_InviteEmailTextBox.Text = failedEmails.Trim( DELIMITERS );
							m_HiveManager.SelectHive( m_SelectedHive, this );
						}
					}
				}
				if( e.Result == RegistryResult.Success ) this.Close();
				else // show the result message for warnings and errors
				{
					AlertUser( e.ResultMessage ); // show result message
					EnableInterface(); // allow user to try input again
				}
			}
		}
		
		private void m_CancelButton_Click( object sender, EventArgs e )
		{
			this.Close(); // will unbind RegistryEventHandler
		}

		/// <summary>Parses {,;} delimited string of emails</summary>
		/// <param name="rawEmails">Delimited string of emails</param>
		/// <param name="parsedEmails">Array of properly formatted emails
		/// if method was successful, otherwise the first failed email</param>
		/// <returns>True if valid emails were parsed, otherwise false</returns>
		private bool ParseEmailList( string rawEmails, out string[] parsedEmails )
		{			
			ArrayList emailList = new ArrayList();
			string[] splitEmails = rawEmails.Split( DELIMITERS );
			
			foreach( string rawEmail in splitEmails )
			{
				string email = rawEmail.Trim();
				if( email.Length > 0 ) // email exists
				{
					if( !User.CheckEmailFormat( email ) ) 
					{
						parsedEmails = new string[]{ email };
						return false; // abort on first failure
					}
					else emailList.Add( email ); // save good emails
				}
			}
			parsedEmails = (string[])emailList.ToArray( typeof(string) );
			return ( parsedEmails.Length > 0 );
		}

		public void SelectHive( HiveModel hive )
		{
			if( m_HiveComboBox.Items.Contains( hive ) )
				m_HiveComboBox.SelectedItem = hive;
			else if( m_HiveComboBox.Items.Count > 0 )
				m_HiveComboBox.SelectedIndex = 0;
		}

		private void EnableInterface( )
		{
			m_AcceptButton.Visible = true;
			m_HiveComboBox.Enabled = true;		
			m_InviteGroupBox.Enabled = true;
		}

		private void DisableInterface( )
		{
			m_AcceptButton.Visible = false;
			m_HiveComboBox.Enabled = false;
			m_InviteGroupBox.Enabled = false;			
		}

		#region NUnit Automated Test Cases

		[TestFixture] public class MemberEditorTest
		{
			[SetUp] public void SetUp() { }

			[TearDown] public void TearDown() { }
			
			[Test] public void ParseEmailListTest()
			{
				MemberEditor editor = new MemberEditor();
				string[] parsedEmails; // out parameter for method
				string rawEmails = ",a@b.com ,, b@c.com ; @invalid ,; e@f.com;";

				bool success = editor.ParseEmailList( rawEmails, out parsedEmails );
				Assert.IsFalse( success, "Parsed an invalid email: " + rawEmails );
				Assert.AreEqual( "@invalid", parsedEmails[0], "Got incorrect failed email" );

				// specify an empty string
				success = editor.ParseEmailList( "", out parsedEmails );
				Assert.IsFalse( success, "Parsed emails from empty string" );
				Assert.AreEqual( 0, parsedEmails.Length, "Got incorrect email count" );

				// specify a string with whitespace chars only
				success = editor.ParseEmailList( "	 ", out parsedEmails );
				Assert.IsFalse( success, "Parsed emails from whitespace string" );
				Assert.AreEqual( 0, parsedEmails.Length, "Got incorrect email count" );

				// specify a string with delimiter chars only
				success = editor.ParseEmailList( ",;;,;", out parsedEmails );
				Assert.IsFalse( success, "Parsed emails from delimiter-only string" );
				Assert.AreEqual( 0, parsedEmails.Length, "Got incorrect email count" );

				// specify an awkward delimited string of otherwise valid emails
				rawEmails = ",;	okarim@buzm.com ,; ,omkarim@yahoo.com,okarim@soaz.com;";
				
				success = editor.ParseEmailList( rawEmails, out parsedEmails );
				Assert.IsTrue( success, "Failed parse on valid emails: " + rawEmails );
				Assert.AreEqual( 3, parsedEmails.Length, "Got incorrect email count" );
				Assert.AreEqual( "okarim@buzm.com", parsedEmails[0], "Got incorrect valid email" );
				Assert.AreEqual( "omkarim@yahoo.com", parsedEmails[1], "Got incorrect valid email" );
				Assert.AreEqual( "okarim@soaz.com", parsedEmails[2], "Got incorrect valid email" );				
			}
		}

		#endregion
	}
}

