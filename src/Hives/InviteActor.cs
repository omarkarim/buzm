using System;
using System.IO;
using System.Xml;
using Buzm.Register;
using Buzm.Utility;

namespace Buzm.Hives
{
	/// <summary>Submits an invitation file to the 
	/// registry for validation and processing </summary>
	public class InviteActor : RegistryActor
	{
		private User m_HiveUser;
		private string m_HiveGuid;
		private string m_InviteFile;
		private HiveManager m_HiveManager;
		private System.Windows.Forms.Label m_LoadingLabel;
		private System.ComponentModel.IContainer components = null;

		public InviteActor( string inviteFile, User user, HiveManager manager )
		{
			m_HiveUser = user;
			m_HiveManager = manager; 
			m_InviteFile = inviteFile;
			
			InitializeComponent(); // forms code
			Action = RegistryAction.AcceptInvite;
		}

		protected override bool SetupRegistryRequest( )
		{
			if( File.Exists( m_InviteFile ) ) // if invite file exists
			{
				SafeXmlDoc inviteDoc = new SafeXmlDoc(); // wrapper doc
				if( inviteDoc.LoadFromFile( m_InviteFile, "InviteActor" ) )
				{									
					// extract hive properties from the xml doc if loaded successfully
					m_HiveGuid = inviteDoc.GetInnerText( "/invite/hive/guid", "InviteActor" );				
					string hiveHost = inviteDoc.GetInnerText( "/invite/hive/host", "InviteActor" );
					string hiveName = inviteDoc.GetInnerText( "/invite/hive/name", "InviteActor" );					
										
					if( m_HiveGuid != String.Empty ) // if hive guid extracted successfully
					{
						// if hive with the same guid is not already present
						if( !m_HiveManager.HiveModels.Contains( m_HiveGuid ) )
						{ 
							// setup action from logged in user
							ActionUser = m_HiveUser.CloneIdentity();
							ActionUser.Guid = inviteDoc.GetInnerText( "/invite/guid", "InviteActor" );
							ActionText = "Processing invitation to Hive: " + hiveName; // set message
						
							// setup hive model based on extracted properties
							HiveModel hiveModel = new HiveModel( hiveName, m_HiveGuid );
							hiveModel.Host = hiveHost; // buzm user that sent invite						
							ActionUser.SetHive( hiveModel.ConfigToXml() );
							return true;
						}
						else
						{
							AlertUser( "You are already subscribed to the Hive: " + hiveName );
							return false;
						}
					}
				}
				// invitation file parse failed if code reached here
				AlertUser( "The invitation file appears to be invalid." );
			}	
			else AlertUser( "Could not find invitation file: " + m_InviteFile );
			return false; // setup failed if the code has reached this point
		}

		public override void RegistryEditor_RegistryResponse( object sender, RegistryEventArgs e )
		{
			if( EndRegistryRequest( e ) ) // if request complete
			{
				if( e.Result == RegistryResult.Success ) // and successful
				{
					User regUser = e.User; // get user returned by registry
					if( ( regUser != null ) && ( m_HiveGuid != String.Empty ) ) 
					{
						XmlNode hiveNode = regUser.GetHive( m_HiveGuid );
						if( hiveNode != null ) // if hive node exists
						{
							Update(); // redraw so loading label can show
							HiveModel hive = m_HiveManager.AddHive( hiveNode );
							if( hive != null ) m_HiveManager.SelectHive( hive, this );				
						}
						else AlertUser( "Could not process configuration for new Hive." );
					}
					else AlertUser( "Could not register invitation - Please try again." );
				}
				else AlertUser( e.ResultMessage ); // show registry error message
				Close(); // hide and cleanup form resources when finished
			}
		}

		/// <summary>Clean up any resources being used</summary>
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
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(InviteActor));
			this.m_LoadingLabel = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// m_ActionProgressBar
			// 
			this.m_ActionProgressBar.Name = "m_ActionProgressBar";
			// 
			// m_LoadingLabel
			// 
			this.m_LoadingLabel.Location = new System.Drawing.Point(8, 24);
			this.m_LoadingLabel.Name = "m_LoadingLabel";
			this.m_LoadingLabel.Size = new System.Drawing.Size(264, 24);
			this.m_LoadingLabel.TabIndex = 3;
			this.m_LoadingLabel.Text = "Please wait while the Hive is setup...";
			this.m_LoadingLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// InviteActor
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 14);
			this.ClientSize = new System.Drawing.Size(386, 58);
			this.Controls.Add(this.m_LoadingLabel);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "InviteActor";
			this.Text = "New Invite - Buzm";
			this.Controls.SetChildIndex(this.m_LoadingLabel, 0);
			this.Controls.SetChildIndex(this.m_ActionProgressBar, 0);
			this.ResumeLayout(false);

		}
		#endregion
	}
}

