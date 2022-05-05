using System;
using System.Xml;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using NUnit.Framework;
using Buzm.Register;
using Buzm.Utility;

namespace Buzm.Hives
{
	/// <summary>UI for editing Hives</summary>
	public class HiveEditor : RegistryEditor
	{ 		
		private User m_HiveUser;
		private HiveModel m_HiveModel;		
		private HiveManager m_HiveManager;	
		private bool m_IsWorkflowComplete;

		private System.Drawing.Color m_SkinSelectedColor;
		private System.Drawing.Color m_SkinUnselectedColor;
		private System.Windows.Forms.TextBox m_NameTextBox;
		private System.Windows.Forms.Label m_NameLabel;
		private System.Windows.Forms.GroupBox m_SkinGroupBox;
		private System.Windows.Forms.Panel m_0x0SkinBorderPanel;
		private System.Windows.Forms.Panel m_0x0SkinShadowPanel;
		private System.Windows.Forms.PictureBox m_0x0SkinPictureBox;		
		private System.Windows.Forms.Panel m_1x0SkinBorderPanel;		
		private System.Windows.Forms.Panel m_1x0SkinShadowPanel;
		private System.Windows.Forms.PictureBox m_1x0SkinPictureBox;
		private System.Windows.Forms.Panel m_0x1SkinBorderPanel;		
		private System.Windows.Forms.Panel m_0x1SkinShadowPanel;
		private System.Windows.Forms.PictureBox m_0x1SkinPictureBox;		
		private System.Windows.Forms.Panel m_1x1SkinBorderPanel;		
		private System.Windows.Forms.Panel m_1x1SkinShadowPanel;
		private System.Windows.Forms.PictureBox m_1x1SkinPictureBox;
		private System.Windows.Forms.Button m_CancelButton;
		private System.Windows.Forms.Button m_AcceptButton;
		private System.ComponentModel.Container components = null;
		private System.Windows.Forms.Label m_LoadingLabel;
		private System.Windows.Forms.GroupBox m_InviteGroupBox;
		private System.Windows.Forms.Label m_InviteMessageSubLabel;
		private System.Windows.Forms.Label m_InviteEmailSubLabel;
		private System.Windows.Forms.Label m_InviteMessageLabel;
		private System.Windows.Forms.Label m_InviteEmailLabel;
		private System.Windows.Forms.TextBox m_InviteMessageTextBox;
		private System.Windows.Forms.TextBox m_InviteEmailTextBox;
		private System.Windows.Forms.Button m_BackButton;
		private System.Windows.Forms.PictureBox m_CurrentSkinPictureBox;

		// default button titles depending on function
		private const string NEXT_BUTTON_TITLE = "Next >";
		private const string SUBMIT_BUTTON_TITLE = "Add Hive";

		public HiveEditor( ){ } // test framework constructor
		public HiveEditor( User hiveUser, HiveManager manager )
		{
			m_HiveUser = hiveUser;
			m_HiveManager = manager; 			

			Action = RegistryAction.InsertHive;						
			InitializeComponent(); // setup interface

			m_CurrentSkinPictureBox = m_0x0SkinPictureBox;
			m_SkinSelectedColor = Color.FromArgb( 218, 113, 21 );
			m_SkinUnselectedColor = Color.FromArgb( 225, 238, 238 );			

			// guids hardcoded for now - should be loaded dynamically
			m_0x0SkinPictureBox.Tag = "3a1edc35-f826-46a0-b3bb-6005ddeae775";
			m_1x0SkinPictureBox.Tag = "61dea5dd-8490-4ce7-812a-27c30f9da0ac";			
			m_0x1SkinPictureBox.Tag = "4055aa8f-b819-4516-8dfb-69c5c573f907";
			m_1x1SkinPictureBox.Tag = "3633bcc2-faef-4ee0-bbee-e00f03fefeaf";

			// invite workflow has been disabled and moved to MemberEditor
			m_IsWorkflowComplete = true; // skip send invite user interface
			m_InviteMessageTextBox.Text = String.Empty; // clear invite text
			m_AcceptButton.Text = SUBMIT_BUTTON_TITLE; // show submit button
		}

		private void m_AcceptButton_Click( object sender, System.EventArgs e )
		{
			if( m_IsWorkflowComplete ) // submit form
			{
				string hiveName = m_NameTextBox.Text;
				if( hiveName.Length > 0 ) // name specified
				{	
					string[] parsedEmails; // valid email invites
					string inviteEmails = m_InviteEmailTextBox.Text;
					if( ParseEmailList( inviteEmails, out parsedEmails ) )
					{
						DisableInterface(); // prevent resubmit
						string hiveGuid = Guid.NewGuid().ToString();
						
						// create new hive and set required properties
						m_HiveModel = new HiveModel( hiveName, hiveGuid );						
						m_HiveModel.SkinGuid = m_CurrentSkinPictureBox.Tag.ToString();
						m_HiveModel.InviteText = m_InviteMessageTextBox.Text;
						m_HiveModel.CreateDate = DateTime.Now; 

						// clone active user and add new hive
						ActionUser = m_HiveUser.CloneIdentity(); 
						ActionUser.SetHive( m_HiveModel.ConfigToXml() );

						// add requested member invites to the hive
						foreach( string inviteEmail in parsedEmails )
						{
							User inviteUser = new User();							
							inviteUser.Email = inviteEmail;
							inviteUser.Guid = Guid.NewGuid().ToString();							
							ActionUser.SetMember( hiveGuid, inviteUser.ToXmlString() );
						}

						// submit new hive and invites to the registry for processing
						BeginRegistryRequest(); // begin asynchronous registry update
					}
					else // valid email addresses could not be parsed from user input
					{
						if( parsedEmails.Length == 0 ) AlertUser( "Please remove any email address text" );
						else AlertUser( "Please correct the following email address: " + parsedEmails[0] );
					}
				}
				else AlertUser( "Please enter a name for your Hive." );	
			}
			else // advance workflow to next page
			{
				m_IsWorkflowComplete = true;
				m_BackButton.Visible = true;
				m_InviteGroupBox.Enabled = true;
				m_InviteGroupBox.BringToFront();				
				m_AcceptButton.Text = SUBMIT_BUTTON_TITLE;
			}
		}

		/// <summary>Parses comma or semicolon separated string 
		/// of emails into individual email addresses </summary>
		/// <param name="rawEmails">Unparsed string of emails</param>
		/// <param name="parsedEmails">Array of properly formatted emails
		/// if method was successful, otherwise the first failed email</param>
		/// <returns>True if the method was successful, otherwise false</returns>
		private bool ParseEmailList( string rawEmails, out string[] parsedEmails )
		{
			char[] delimiters = new char[]{ ',' , ';' };
			string[] splitEmails = rawEmails.Split( delimiters );

			ArrayList emailList = new ArrayList();
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
			return true; // indicate that the parse process was successful
		}

		public override void RegistryEditor_RegistryResponse( object sender, RegistryEventArgs e )
		{
			if( EndRegistryRequest( e ) ) // if request complete
			{
				if( e.Result == RegistryResult.Success ) // and successful
				{
					if( m_HiveModel != null ) // ensure hive model is still set
					{
						m_LoadingLabel.Visible = true; // notify user of hive loading
						this.Update(); // force form to redraw before adding the hive

						SafeXmlDoc hiveDoc = new SafeXmlDoc( m_HiveModel.ConfigToXml() );
						XmlNode hiveNode = hiveDoc.GetNode( "/hive", "HiveEditor" );
						if( hiveNode != null ) // if the config node was found
						{
							// add hive to manager and select it for viewing
							HiveModel hive = m_HiveManager.AddHive( hiveNode );
							if( hive != null ) m_HiveManager.SelectHive( hive, this );
						}
					}
					this.Close(); // hide form - m_HiveModel likely to never be null
				}
				else 
				{
					AlertUser( e.ResultMessage ); // show error message
					EnableInterface(); // allow user to try input again
				}
			}
		}

		private void SkinPictureBox_Click( object sender, System.EventArgs e )
		{
			// if sender is not already selected
			if( sender != m_CurrentSkinPictureBox )
			{
				PictureBox skinPictureBox = (PictureBox)sender;			
				skinPictureBox.Parent.BackColor = m_SkinSelectedColor;
				m_CurrentSkinPictureBox.Parent.BackColor = m_SkinUnselectedColor;
				m_CurrentSkinPictureBox = skinPictureBox; // set selected skin
			}
		}

		private void m_BackButton_Click(object sender, System.EventArgs e)
		{
			m_IsWorkflowComplete = false;
			m_BackButton.Visible = false;
			m_SkinGroupBox.BringToFront();
			m_InviteGroupBox.Enabled = false;
			m_AcceptButton.Text = NEXT_BUTTON_TITLE;
		}

		private void m_CancelButton_Click( object sender, System.EventArgs e )
		{
			this.Close();
		}

		private void EnableInterface( )
		{
			m_NameTextBox.Enabled = true;
			// m_InviteGroupBox.Enabled = true;
			m_SkinGroupBox.Enabled = true;
			m_AcceptButton.Visible = true;
			// m_BackButton.Visible = true;
			m_AcceptButton.Focus();
		}

		private void DisableInterface( )
		{
			m_NameTextBox.Enabled = false;
			m_AcceptButton.Visible = false;
			m_InviteGroupBox.Enabled = false;
			m_SkinGroupBox.Enabled = false;			
			m_BackButton.Visible = false;
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HiveEditor));
			this.m_NameTextBox = new System.Windows.Forms.TextBox();
			this.m_NameLabel = new System.Windows.Forms.Label();
			this.m_SkinGroupBox = new System.Windows.Forms.GroupBox();
			this.m_1x1SkinBorderPanel = new System.Windows.Forms.Panel();
			this.m_1x1SkinPictureBox = new System.Windows.Forms.PictureBox();
			this.m_1x1SkinShadowPanel = new System.Windows.Forms.Panel();
			this.m_0x1SkinBorderPanel = new System.Windows.Forms.Panel();
			this.m_0x1SkinPictureBox = new System.Windows.Forms.PictureBox();
			this.m_0x1SkinShadowPanel = new System.Windows.Forms.Panel();
			this.m_1x0SkinBorderPanel = new System.Windows.Forms.Panel();
			this.m_1x0SkinPictureBox = new System.Windows.Forms.PictureBox();
			this.m_1x0SkinShadowPanel = new System.Windows.Forms.Panel();
			this.m_0x0SkinBorderPanel = new System.Windows.Forms.Panel();
			this.m_0x0SkinPictureBox = new System.Windows.Forms.PictureBox();
			this.m_0x0SkinShadowPanel = new System.Windows.Forms.Panel();
			this.m_CancelButton = new System.Windows.Forms.Button();
			this.m_AcceptButton = new System.Windows.Forms.Button();
			this.m_LoadingLabel = new System.Windows.Forms.Label();
			this.m_InviteGroupBox = new System.Windows.Forms.GroupBox();
			this.m_InviteMessageSubLabel = new System.Windows.Forms.Label();
			this.m_InviteEmailSubLabel = new System.Windows.Forms.Label();
			this.m_InviteMessageLabel = new System.Windows.Forms.Label();
			this.m_InviteEmailLabel = new System.Windows.Forms.Label();
			this.m_InviteMessageTextBox = new System.Windows.Forms.TextBox();
			this.m_InviteEmailTextBox = new System.Windows.Forms.TextBox();
			this.m_BackButton = new System.Windows.Forms.Button();
			this.m_SkinGroupBox.SuspendLayout();
			this.m_1x1SkinBorderPanel.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.m_1x1SkinPictureBox)).BeginInit();
			this.m_0x1SkinBorderPanel.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.m_0x1SkinPictureBox)).BeginInit();
			this.m_1x0SkinBorderPanel.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.m_1x0SkinPictureBox)).BeginInit();
			this.m_0x0SkinBorderPanel.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.m_0x0SkinPictureBox)).BeginInit();
			this.m_InviteGroupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// m_ActionProgressBar
			// 
			this.m_ActionProgressBar.Location = new System.Drawing.Point(8, 384);
			this.m_ActionProgressBar.Size = new System.Drawing.Size(284, 24);
			// 
			// m_NameTextBox
			// 
			this.m_NameTextBox.Location = new System.Drawing.Point(8, 24);
			this.m_NameTextBox.MaxLength = 1000;
			this.m_NameTextBox.Name = "m_NameTextBox";
			this.m_NameTextBox.Size = new System.Drawing.Size(392, 21);
			this.m_NameTextBox.TabIndex = 1;
			// 
			// m_NameLabel
			// 
			this.m_NameLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_NameLabel.Location = new System.Drawing.Point(8, 8);
			this.m_NameLabel.Name = "m_NameLabel";
			this.m_NameLabel.Size = new System.Drawing.Size(296, 16);
			this.m_NameLabel.TabIndex = 2;
			this.m_NameLabel.Text = "Name your Hive:";
			// 
			// m_SkinGroupBox
			// 
			this.m_SkinGroupBox.Controls.Add(this.m_1x1SkinBorderPanel);
			this.m_SkinGroupBox.Controls.Add(this.m_1x1SkinShadowPanel);
			this.m_SkinGroupBox.Controls.Add(this.m_0x1SkinBorderPanel);
			this.m_SkinGroupBox.Controls.Add(this.m_0x1SkinShadowPanel);
			this.m_SkinGroupBox.Controls.Add(this.m_1x0SkinBorderPanel);
			this.m_SkinGroupBox.Controls.Add(this.m_1x0SkinShadowPanel);
			this.m_SkinGroupBox.Controls.Add(this.m_0x0SkinBorderPanel);
			this.m_SkinGroupBox.Controls.Add(this.m_0x0SkinShadowPanel);
			this.m_SkinGroupBox.Location = new System.Drawing.Point(8, 56);
			this.m_SkinGroupBox.Name = "m_SkinGroupBox";
			this.m_SkinGroupBox.Size = new System.Drawing.Size(392, 320);
			this.m_SkinGroupBox.TabIndex = 2;
			this.m_SkinGroupBox.TabStop = false;
			this.m_SkinGroupBox.Text = "Choose a Template";
			// 
			// m_1x1SkinBorderPanel
			// 
			this.m_1x1SkinBorderPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(238)))), ((int)(((byte)(238)))));
			this.m_1x1SkinBorderPanel.Controls.Add(this.m_1x1SkinPictureBox);
			this.m_1x1SkinBorderPanel.Location = new System.Drawing.Point(208, 176);
			this.m_1x1SkinBorderPanel.Name = "m_1x1SkinBorderPanel";
			this.m_1x1SkinBorderPanel.Padding = new System.Windows.Forms.Padding(2);
			this.m_1x1SkinBorderPanel.Size = new System.Drawing.Size(164, 124);
			this.m_1x1SkinBorderPanel.TabIndex = 9;
			// 
			// m_1x1SkinPictureBox
			// 
			this.m_1x1SkinPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_1x1SkinPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_1x1SkinPictureBox.Image")));
			this.m_1x1SkinPictureBox.Location = new System.Drawing.Point(2, 2);
			this.m_1x1SkinPictureBox.Name = "m_1x1SkinPictureBox";
			this.m_1x1SkinPictureBox.Size = new System.Drawing.Size(160, 120);
			this.m_1x1SkinPictureBox.TabIndex = 0;
			this.m_1x1SkinPictureBox.TabStop = false;
			this.m_1x1SkinPictureBox.Click += new System.EventHandler(this.SkinPictureBox_Click);
			// 
			// m_1x1SkinShadowPanel
			// 
			this.m_1x1SkinShadowPanel.BackColor = System.Drawing.SystemColors.ControlDarkDark;
			this.m_1x1SkinShadowPanel.Location = new System.Drawing.Point(216, 184);
			this.m_1x1SkinShadowPanel.Name = "m_1x1SkinShadowPanel";
			this.m_1x1SkinShadowPanel.Size = new System.Drawing.Size(164, 124);
			this.m_1x1SkinShadowPanel.TabIndex = 10;
			// 
			// m_0x1SkinBorderPanel
			// 
			this.m_0x1SkinBorderPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(238)))), ((int)(((byte)(238)))));
			this.m_0x1SkinBorderPanel.Controls.Add(this.m_0x1SkinPictureBox);
			this.m_0x1SkinBorderPanel.Location = new System.Drawing.Point(16, 176);
			this.m_0x1SkinBorderPanel.Name = "m_0x1SkinBorderPanel";
			this.m_0x1SkinBorderPanel.Padding = new System.Windows.Forms.Padding(2);
			this.m_0x1SkinBorderPanel.Size = new System.Drawing.Size(164, 124);
			this.m_0x1SkinBorderPanel.TabIndex = 7;
			// 
			// m_0x1SkinPictureBox
			// 
			this.m_0x1SkinPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_0x1SkinPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_0x1SkinPictureBox.Image")));
			this.m_0x1SkinPictureBox.Location = new System.Drawing.Point(2, 2);
			this.m_0x1SkinPictureBox.Name = "m_0x1SkinPictureBox";
			this.m_0x1SkinPictureBox.Size = new System.Drawing.Size(160, 120);
			this.m_0x1SkinPictureBox.TabIndex = 0;
			this.m_0x1SkinPictureBox.TabStop = false;
			this.m_0x1SkinPictureBox.Click += new System.EventHandler(this.SkinPictureBox_Click);
			// 
			// m_0x1SkinShadowPanel
			// 
			this.m_0x1SkinShadowPanel.BackColor = System.Drawing.SystemColors.ControlDarkDark;
			this.m_0x1SkinShadowPanel.Location = new System.Drawing.Point(24, 184);
			this.m_0x1SkinShadowPanel.Name = "m_0x1SkinShadowPanel";
			this.m_0x1SkinShadowPanel.Size = new System.Drawing.Size(164, 124);
			this.m_0x1SkinShadowPanel.TabIndex = 8;
			// 
			// m_1x0SkinBorderPanel
			// 
			this.m_1x0SkinBorderPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(238)))), ((int)(((byte)(238)))));
			this.m_1x0SkinBorderPanel.Controls.Add(this.m_1x0SkinPictureBox);
			this.m_1x0SkinBorderPanel.Location = new System.Drawing.Point(208, 24);
			this.m_1x0SkinBorderPanel.Name = "m_1x0SkinBorderPanel";
			this.m_1x0SkinBorderPanel.Padding = new System.Windows.Forms.Padding(2);
			this.m_1x0SkinBorderPanel.Size = new System.Drawing.Size(164, 124);
			this.m_1x0SkinBorderPanel.TabIndex = 5;
			// 
			// m_1x0SkinPictureBox
			// 
			this.m_1x0SkinPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_1x0SkinPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_1x0SkinPictureBox.Image")));
			this.m_1x0SkinPictureBox.Location = new System.Drawing.Point(2, 2);
			this.m_1x0SkinPictureBox.Name = "m_1x0SkinPictureBox";
			this.m_1x0SkinPictureBox.Size = new System.Drawing.Size(160, 120);
			this.m_1x0SkinPictureBox.TabIndex = 0;
			this.m_1x0SkinPictureBox.TabStop = false;
			this.m_1x0SkinPictureBox.Click += new System.EventHandler(this.SkinPictureBox_Click);
			// 
			// m_1x0SkinShadowPanel
			// 
			this.m_1x0SkinShadowPanel.BackColor = System.Drawing.SystemColors.ControlDarkDark;
			this.m_1x0SkinShadowPanel.Location = new System.Drawing.Point(216, 32);
			this.m_1x0SkinShadowPanel.Name = "m_1x0SkinShadowPanel";
			this.m_1x0SkinShadowPanel.Size = new System.Drawing.Size(164, 124);
			this.m_1x0SkinShadowPanel.TabIndex = 6;
			// 
			// m_0x0SkinBorderPanel
			// 
			this.m_0x0SkinBorderPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(218)))), ((int)(((byte)(113)))), ((int)(((byte)(21)))));
			this.m_0x0SkinBorderPanel.Controls.Add(this.m_0x0SkinPictureBox);
			this.m_0x0SkinBorderPanel.Location = new System.Drawing.Point(16, 24);
			this.m_0x0SkinBorderPanel.Name = "m_0x0SkinBorderPanel";
			this.m_0x0SkinBorderPanel.Padding = new System.Windows.Forms.Padding(2);
			this.m_0x0SkinBorderPanel.Size = new System.Drawing.Size(164, 124);
			this.m_0x0SkinBorderPanel.TabIndex = 3;
			// 
			// m_0x0SkinPictureBox
			// 
			this.m_0x0SkinPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_0x0SkinPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_0x0SkinPictureBox.Image")));
			this.m_0x0SkinPictureBox.Location = new System.Drawing.Point(2, 2);
			this.m_0x0SkinPictureBox.Name = "m_0x0SkinPictureBox";
			this.m_0x0SkinPictureBox.Size = new System.Drawing.Size(160, 120);
			this.m_0x0SkinPictureBox.TabIndex = 0;
			this.m_0x0SkinPictureBox.TabStop = false;
			this.m_0x0SkinPictureBox.Click += new System.EventHandler(this.SkinPictureBox_Click);
			// 
			// m_0x0SkinShadowPanel
			// 
			this.m_0x0SkinShadowPanel.BackColor = System.Drawing.SystemColors.ControlDarkDark;
			this.m_0x0SkinShadowPanel.Location = new System.Drawing.Point(24, 32);
			this.m_0x0SkinShadowPanel.Name = "m_0x0SkinShadowPanel";
			this.m_0x0SkinShadowPanel.Size = new System.Drawing.Size(164, 124);
			this.m_0x0SkinShadowPanel.TabIndex = 4;
			// 
			// m_CancelButton
			// 
			this.m_CancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.m_CancelButton.Location = new System.Drawing.Point(300, 384);
			this.m_CancelButton.Name = "m_CancelButton";
			this.m_CancelButton.Size = new System.Drawing.Size(100, 25);
			this.m_CancelButton.TabIndex = 9;
			this.m_CancelButton.Text = "Cancel";
			this.m_CancelButton.Click += new System.EventHandler(this.m_CancelButton_Click);
			// 
			// m_AcceptButton
			// 
			this.m_AcceptButton.Location = new System.Drawing.Point(192, 384);
			this.m_AcceptButton.Name = "m_AcceptButton";
			this.m_AcceptButton.Size = new System.Drawing.Size(100, 25);
			this.m_AcceptButton.TabIndex = 5;
			this.m_AcceptButton.Text = "Next >";
			this.m_AcceptButton.Click += new System.EventHandler(this.m_AcceptButton_Click);
			// 
			// m_LoadingLabel
			// 
			this.m_LoadingLabel.Location = new System.Drawing.Point(8, 384);
			this.m_LoadingLabel.Name = "m_LoadingLabel";
			this.m_LoadingLabel.Size = new System.Drawing.Size(192, 24);
			this.m_LoadingLabel.TabIndex = 6;
			this.m_LoadingLabel.Text = "Please wait while the Hive is setup...";
			this.m_LoadingLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.m_LoadingLabel.Visible = false;
			// 
			// m_InviteGroupBox
			// 
			this.m_InviteGroupBox.Controls.Add(this.m_InviteMessageSubLabel);
			this.m_InviteGroupBox.Controls.Add(this.m_InviteEmailSubLabel);
			this.m_InviteGroupBox.Controls.Add(this.m_InviteMessageLabel);
			this.m_InviteGroupBox.Controls.Add(this.m_InviteEmailLabel);
			this.m_InviteGroupBox.Controls.Add(this.m_InviteMessageTextBox);
			this.m_InviteGroupBox.Controls.Add(this.m_InviteEmailTextBox);
			this.m_InviteGroupBox.Enabled = false;
			this.m_InviteGroupBox.Location = new System.Drawing.Point(8, 56);
			this.m_InviteGroupBox.Name = "m_InviteGroupBox";
			this.m_InviteGroupBox.Size = new System.Drawing.Size(392, 320);
			this.m_InviteGroupBox.TabIndex = 4;
			this.m_InviteGroupBox.TabStop = false;
			this.m_InviteGroupBox.Text = "Invite your Friends";
			// 
			// m_InviteMessageSubLabel
			// 
			this.m_InviteMessageSubLabel.AutoSize = true;
			this.m_InviteMessageSubLabel.Font = new System.Drawing.Font("Tahoma", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_InviteMessageSubLabel.Location = new System.Drawing.Point(16, 174);
			this.m_InviteMessageSubLabel.Name = "m_InviteMessageSubLabel";
			this.m_InviteMessageSubLabel.Size = new System.Drawing.Size(326, 11);
			this.m_InviteMessageSubLabel.TabIndex = 5;
			this.m_InviteMessageSubLabel.Text = "(We will add some instructional text so your friends know how to setup Buzm)";
			this.m_InviteMessageSubLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_InviteEmailSubLabel
			// 
			this.m_InviteEmailSubLabel.AutoSize = true;
			this.m_InviteEmailSubLabel.Font = new System.Drawing.Font("Tahoma", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_InviteEmailSubLabel.Location = new System.Drawing.Point(16, 46);
			this.m_InviteEmailSubLabel.Name = "m_InviteEmailSubLabel";
			this.m_InviteEmailSubLabel.Size = new System.Drawing.Size(307, 11);
			this.m_InviteEmailSubLabel.TabIndex = 4;
			this.m_InviteEmailSubLabel.Text = "(Leave blank if you want to keep this Hive private and click “Add Hive”)";
			this.m_InviteEmailSubLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_InviteMessageLabel
			// 
			this.m_InviteMessageLabel.AutoSize = true;
			this.m_InviteMessageLabel.Location = new System.Drawing.Point(16, 157);
			this.m_InviteMessageLabel.Name = "m_InviteMessageLabel";
			this.m_InviteMessageLabel.Size = new System.Drawing.Size(194, 13);
			this.m_InviteMessageLabel.TabIndex = 3;
			this.m_InviteMessageLabel.Text = "Add a personal message or leave as is:";
			this.m_InviteMessageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_InviteEmailLabel
			// 
			this.m_InviteEmailLabel.AutoSize = true;
			this.m_InviteEmailLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_InviteEmailLabel.Location = new System.Drawing.Point(16, 29);
			this.m_InviteEmailLabel.Name = "m_InviteEmailLabel";
			this.m_InviteEmailLabel.Size = new System.Drawing.Size(224, 13);
			this.m_InviteEmailLabel.TabIndex = 2;
			this.m_InviteEmailLabel.Text = "Enter email addresses separated by commas:";
			this.m_InviteEmailLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_InviteMessageTextBox
			// 
			this.m_InviteMessageTextBox.AcceptsReturn = true;
			this.m_InviteMessageTextBox.Location = new System.Drawing.Point(16, 190);
			this.m_InviteMessageTextBox.MaxLength = 5000;
			this.m_InviteMessageTextBox.Multiline = true;
			this.m_InviteMessageTextBox.Name = "m_InviteMessageTextBox";
			this.m_InviteMessageTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.m_InviteMessageTextBox.Size = new System.Drawing.Size(360, 109);
			this.m_InviteMessageTextBox.TabIndex = 1;
			// 
			// m_InviteEmailTextBox
			// 
			this.m_InviteEmailTextBox.AcceptsReturn = true;
			this.m_InviteEmailTextBox.Location = new System.Drawing.Point(16, 62);
			this.m_InviteEmailTextBox.MaxLength = 1000;
			this.m_InviteEmailTextBox.Multiline = true;
			this.m_InviteEmailTextBox.Name = "m_InviteEmailTextBox";
			this.m_InviteEmailTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.m_InviteEmailTextBox.Size = new System.Drawing.Size(360, 80);
			this.m_InviteEmailTextBox.TabIndex = 0;
			// 
			// m_BackButton
			// 
			this.m_BackButton.Location = new System.Drawing.Point(84, 384);
			this.m_BackButton.Name = "m_BackButton";
			this.m_BackButton.Size = new System.Drawing.Size(100, 25);
			this.m_BackButton.TabIndex = 10;
			this.m_BackButton.Text = "<  Back";
			this.m_BackButton.Visible = false;
			this.m_BackButton.Click += new System.EventHandler(this.m_BackButton_Click);
			// 
			// HiveEditor
			// 
			this.AcceptButton = this.m_AcceptButton;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 14);
			this.CancelButton = this.m_CancelButton;
			this.ClientSize = new System.Drawing.Size(408, 416);
			this.Controls.Add(this.m_BackButton);
			this.Controls.Add(this.m_AcceptButton);
			this.Controls.Add(this.m_LoadingLabel);
			this.Controls.Add(this.m_CancelButton);
			this.Controls.Add(this.m_NameLabel);
			this.Controls.Add(this.m_NameTextBox);
			this.Controls.Add(this.m_SkinGroupBox);
			this.Controls.Add(this.m_InviteGroupBox);
			this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.Name = "HiveEditor";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Create Hive - Buzm";
			this.Controls.SetChildIndex(this.m_InviteGroupBox, 0);
			this.Controls.SetChildIndex(this.m_SkinGroupBox, 0);
			this.Controls.SetChildIndex(this.m_NameTextBox, 0);
			this.Controls.SetChildIndex(this.m_NameLabel, 0);
			this.Controls.SetChildIndex(this.m_CancelButton, 0);
			this.Controls.SetChildIndex(this.m_ActionProgressBar, 0);
			this.Controls.SetChildIndex(this.m_LoadingLabel, 0);
			this.Controls.SetChildIndex(this.m_AcceptButton, 0);
			this.Controls.SetChildIndex(this.m_BackButton, 0);
			this.m_SkinGroupBox.ResumeLayout(false);
			this.m_1x1SkinBorderPanel.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.m_1x1SkinPictureBox)).EndInit();
			this.m_0x1SkinBorderPanel.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.m_0x1SkinPictureBox)).EndInit();
			this.m_1x0SkinBorderPanel.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.m_1x0SkinPictureBox)).EndInit();
			this.m_0x0SkinBorderPanel.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.m_0x0SkinPictureBox)).EndInit();
			this.m_InviteGroupBox.ResumeLayout(false);
			this.m_InviteGroupBox.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion

		#region NUnit Automated Test Cases

		[TestFixture] public class HiveEditorTest
		{
			[SetUp] public void SetUp() { }

			[TearDown] public void TearDown() { }
			
			[Test] public void ParseEmailListTest()
			{
				HiveEditor editor = new HiveEditor();
				string[] parsedEmails; // out parameter for method
				string rawEmails = ",a@b.com ,, b@c.com ; @invalid ,; e@f.com;";

				bool success = editor.ParseEmailList( rawEmails, out parsedEmails );
				Assertion.Assert( "Parsed an invalid email: " + rawEmails, !success );
				Assertion.AssertEquals( "Got incorrect failed email", "@invalid", parsedEmails[0] );

				// specify an empty string
				success = editor.ParseEmailList( "", out parsedEmails );
				Assertion.Assert( "Parsed an invalid email: " + rawEmails, success );
				Assertion.AssertEquals( "Got incorrect email count", 0, parsedEmails.Length );

				// specify a string with whitespace chars only
				success = editor.ParseEmailList( "	 ", out parsedEmails );
				Assertion.Assert( "Parsed an invalid email: " + rawEmails, success );
				Assertion.AssertEquals( "Got incorrect email count", 0, parsedEmails.Length );

				// specify an awkward delimited string of otherwise valid emails
				rawEmails = ",;	okarim@buzm.com ,; ,omkarim@yahoo.com,okarim@soaz.com;";
				
				success = editor.ParseEmailList( rawEmails, out parsedEmails );
				Assertion.Assert( "Failed parsed on valid emails: " + rawEmails, success );
				Assertion.AssertEquals( "Got incorrect email count", 3, parsedEmails.Length );
				Assertion.AssertEquals( "Got incorrect valid email", "okarim@buzm.com", parsedEmails[0] );
				Assertion.AssertEquals( "Got incorrect valid email", "omkarim@yahoo.com", parsedEmails[1] );
				Assertion.AssertEquals( "Got incorrect valid email", "okarim@soaz.com", parsedEmails[2] );				
			}
		}

		#endregion

	}
}
