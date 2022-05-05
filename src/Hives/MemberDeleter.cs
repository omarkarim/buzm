using System;
using System.Windows.Forms;
using Buzm.Register;
using Buzm.Schemas;

namespace Buzm.Hives
{
	public partial class MemberDeleter : RegistryActor
	{
		private User m_HiveUser;
		private HiveModel m_CurrentHive;
		private UserConfigType m_DeletedMember;

		public MemberDeleter( User user, HiveModel hive, UserConfigType member )
		{
			m_HiveUser = user;
			m_CurrentHive = hive;
			m_DeletedMember = member;
			
			Action = RegistryAction.DeleteMembers;
			InitializeComponent(); // designer code
		}

		protected override bool SetupRegistryRequest()
		{
			string memberName = m_DeletedMember.Login; // extract login or email
			if( String.IsNullOrEmpty( memberName ) ) memberName = m_DeletedMember.Email;

			if( MessageBox.Show( "Are you sure you want to delete '" + memberName + "'?",
				"Confirm Member Delete", MessageBoxButtons.YesNo ) == DialogResult.Yes )
			{
				ActionUser = m_HiveUser.CloneIdentity();
				ActionUser.SetHive( m_CurrentHive.ConfigToXml() );
				ActionUser.SetMember( m_CurrentHive.Guid, m_DeletedMember.ToXml() );
				
				ActionText = "Please wait while member '" + memberName + "' is deleted...";
				return true; // indicate that the delete request has been setup
			}
			else return false; // cancel delete request and close window
		}

		public override void RegistryEditor_RegistryResponse( object sender, RegistryEventArgs e )
		{
			if( EndRegistryRequest( e ) ) // if request complete
			{
				if( e.Result == RegistryResult.Success ) // and successful
				{
					// remove member from hive and notify listeners
					m_CurrentHive.RemoveMember( m_DeletedMember, this );
				}
				else AlertUser( e.ResultMessage ); // show registry error message
				Close(); // hide and cleanup form resources when finished
			}
		}
	}
}

