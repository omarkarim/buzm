using Buzm.Register;
using Buzm.Network.Feeds;
using System.Windows.Forms;

namespace Buzm.Hives
{
	public class FeedDeleter : RegistryActor
	{
		User m_HiveUser;
		HiveModel m_CurrentHive;
		FeedModel m_DeletedFeed;
		private System.ComponentModel.IContainer components = null;

		public FeedDeleter( User user, HiveModel hive, FeedModel feed )
		{
			m_HiveUser = user;
			m_CurrentHive = hive;
			m_DeletedFeed = feed;

			InitializeComponent(); // forms code
			Action = RegistryAction.DeleteFeeds;
		}

		protected override bool SetupRegistryRequest( )
		{											
			if( MessageBox.Show( "Are you sure you want to delete '" + m_DeletedFeed.Name 
				+ "'?", "Confirm Feed Delete", MessageBoxButtons.YesNo ) ==  DialogResult.Yes )
			{
				ActionUser = m_HiveUser.CloneIdentity(); 
				ActionUser.SetHive( m_CurrentHive.ConfigToXml() );
				ActionUser.SetFeed( m_CurrentHive.Guid, m_DeletedFeed.ConfigToXml() );
				ActionText = "Please wait while feed '" + m_DeletedFeed.Name + "' is deleted..."; 
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
					// remove feed from hive to notify feed listeners
					m_CurrentHive.RemoveFeed( m_DeletedFeed, this );
				}
				else AlertUser( e.ResultMessage ); // show registry error message
				Close(); // hide and cleanup form resources when finished
			}
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(FeedDeleter));
			// 
			// m_ActionProgressBar
			// 
			this.m_ActionProgressBar.Name = "m_ActionProgressBar";
			// 
			// FeedDeleter
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 14);
			this.ClientSize = new System.Drawing.Size(386, 58);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FeedDeleter";
			this.Text = "Delete Feed - Buzm";

		}
		#endregion
	}
}

