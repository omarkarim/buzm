using Buzm.Register;
using System.Windows.Forms;

namespace Buzm.Hives
{
	public class HiveDeleter : Buzm.RegistryActor
	{
		User m_HiveUser;
		HiveModel m_HiveModel;
		HiveManager m_HiveManager;
		private System.ComponentModel.IContainer components = null;

		public HiveDeleter( User user, HiveModel hive, HiveManager manager )
		{
			m_HiveUser = user;
			m_HiveModel = hive;
			m_HiveManager = manager;

			InitializeComponent(); // forms code
			Action = RegistryAction.DeleteHive;
		}

		protected override bool SetupRegistryRequest( )
		{											
			if( MessageBox.Show( "Are you sure you want to delete '" + m_HiveModel.Name 
				+ "'?", "Confirm Hive Delete", MessageBoxButtons.YesNo ) ==  DialogResult.Yes )
			{
				ActionUser = m_HiveUser.CloneIdentity(); 
				ActionUser.SetHive( m_HiveModel.ConfigToXml() );
				ActionText = "Please wait while Hive '" + m_HiveModel.Name + "' is deleted..."; 
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
					m_HiveManager.RemoveHive( m_HiveModel ); // unregister hive
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
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(HiveDeleter));
			// 
			// m_ActionProgressBar
			// 
			this.m_ActionProgressBar.Name = "m_ActionProgressBar";
			// 
			// HiveDeleter
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 14);
			this.ClientSize = new System.Drawing.Size(386, 58);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "HiveDeleter";
			this.Text = "Delete Hive - Buzm";

		}
		#endregion
	}
}

