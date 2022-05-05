using System;
using System.Drawing;
using System.Diagnostics;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using Buzm.Register;
using Buzm.Utility;

namespace Buzm
{
	/// <summary>UI for login/registration</summary>
	public class UserEditor : RegistryEditor
	{
		private UserLoginState m_RequestLoginState;
		public event RegistryEventHandler UserActivated;
		public event RegistryEventHandler UserDeactivated;

		// default window titles depending on function
		private const string LOGIN_BUTTON_TITLE = "Login";
		private const string REGISTER_BUTTON_TITLE = "Sign Up";
		private const string LOGIN_FORM_TITLE = "Network Login - Buzm";
		private const string REGISTER_FORM_TITLE = "Sign Up - Buzm";

		private const string PRIVACY_URL = "http://www.buzm.com/help/privacy.html";
		private const string SILENT_LOGIN_ERROR = "Your login credentials have expired. Please sign in again.";

		private System.Windows.Forms.PictureBox m_LogoPictureBox;
		private System.Windows.Forms.Panel m_LoginPanel;
		private System.Windows.Forms.GroupBox m_LoginGroupBox;
		private System.Windows.Forms.Label m_LoginIdLabel;
		private System.Windows.Forms.TextBox m_LoginIdTextBox;
		private System.Windows.Forms.Label m_LoginPassLabel;
		private System.Windows.Forms.TextBox m_LoginPassTextBox;
		private System.Windows.Forms.CheckBox m_LoginRemCheckBox;
		private System.Windows.Forms.Label m_ForgotPassLabel;
		private System.Windows.Forms.LinkLabel m_ForgotPassLinkLabel;
		private System.Windows.Forms.LinkLabel m_NewToBuzmLinkLabel;
		private System.Windows.Forms.LinkLabel m_PrivacyLinkLabel;
		private System.Windows.Forms.Panel m_RegisterPanel;
		private System.Windows.Forms.GroupBox m_RegisterGroupBox;
		private System.Windows.Forms.Label m_RegisterIdLabel;
		private System.Windows.Forms.TextBox m_RegisterIdTextBox;
		private System.Windows.Forms.Label m_RegisterPassLabel;
		private System.Windows.Forms.TextBox m_RegisterPassTextBox;
		private System.Windows.Forms.Label m_RegisterPassAgainLabel;
		private System.Windows.Forms.TextBox m_RegisterPassAgainTextBox;
		private System.Windows.Forms.Label m_RegisterEmailLabel;
		private System.Windows.Forms.TextBox m_RegisterEmailTextBox;
		private System.Windows.Forms.Button m_AcceptButton;
		private System.Windows.Forms.Label m_LoadingLabel;
		private System.Windows.Forms.Button m_CancelButton;
		private System.ComponentModel.IContainer components;
		
		public UserEditor( RegistryAction action, User loginUser, UserLoginState loginState )
		{
			Action = action; // save action in base
			InitializeComponent(); // forms designer

			// login state is manual for most requests
			m_RequestLoginState = UserLoginState.Manual;
			
			// if this is a new user registration
			if( action == RegistryAction.InsertUser )
			{
				this.Text = REGISTER_FORM_TITLE;
				m_AcceptButton.Text = REGISTER_BUTTON_TITLE;

				m_PrivacyLinkLabel.Visible = true;
				m_RegisterPanel.BringToFront();
			}	
			else // this is a normal login request
			{
				this.Text = LOGIN_FORM_TITLE;
				m_AcceptButton.Text = LOGIN_BUTTON_TITLE;
				
				m_LoginPanel.BringToFront();
				SetupLogin( loginUser, loginState );
			}
		}

		public override void RegistryEditor_RegistryResponse( object sender, RegistryEventArgs e )
		{
			if( EndRegistryRequest( e ) ) // if request complete
			{
				if( e.Result == RegistryResult.Success )
				{
					ActionUser = e.User; 
					ActionUser.RememberLogin = true;
					ActionUser.LoginState = m_RequestLoginState;

					string login = ActionUser.Login;
					m_LoadingLabel.Text += login + "...";
				
					// display message & close form
					m_AcceptButton.Visible = false;
					m_PrivacyLinkLabel.Visible = false;
					m_LoadingLabel.Visible = true;
					this.Update(); this.Close();

					// log authentication and registration
					Log.Write( "User " + login + " logged in", 
					TraceLevel.Verbose, "UserEditor.ProcessResult" );

					// fire event to notify clients that the active user has changed					
					RegistryEventArgs regArgs = new RegistryEventArgs( ActionUser, Action, ActionGuid );
					if( UserActivated != null ) UserActivated( this, regArgs ); 
					this.DialogResult = DialogResult.OK; // return success
				}
				else if( ( Action == RegistryAction.LoginUser )
				&& ( m_RequestLoginState == UserLoginState.Silent ) )
				{
					// if silent login credentials were invalid
					if( e.Result == RegistryResult.UserError )
					{
						AlertUser( SILENT_LOGIN_ERROR ); // notify and sign out
						RegistryEventArgs args = new RegistryEventArgs( e.User, Action, ActionGuid );
						if( UserDeactivated != null ) UserDeactivated( this, args );
					}
					this.DialogResult = DialogResult.Abort;
					this.Close(); // cancel login attempt
				}
				else // display error result to user
				{
					AlertUser( e.ResultMessage );
					EnableInterface(); // retry
				}
			}
		}

		// TODO: Add input verification logic here
		private bool ProcessAction( RegistryAction action )
		{
			// if this is a new user registration
			if( action == RegistryAction.InsertUser )
			{
				// extract passwords from text fields
				string password = m_RegisterPassTextBox.Text;
				string passAgain = m_RegisterPassAgainTextBox.Text;

				if( password == passAgain ) // password match
				{
					User actionUser = ActionUser;
					actionUser.Password = password;
					actionUser.Login = m_RegisterIdTextBox.Text;
					actionUser.Email = m_RegisterEmailTextBox.Text;
				}
				else
				{	
					RegistryEventArgs e = new RegistryEventArgs( ActionUser, RegistryResult.UserError,
					"Passwords did not match - Please try entering them again.", ActionGuid );
					RegistryEditor_RegistryResponse( this, e ); // send local response
					return false;
				}
			}	
			else // this is a normal login request
			{
				User actionUser = ActionUser;
				actionUser.Login = m_LoginIdTextBox.Text;
				actionUser.Password = m_LoginPassTextBox.Text;
			}
			return true; // process should have succeeded
		}

		private void SetupLogin( User user, UserLoginState state )
		{
			// user required for setup
			if( user == null ) return;
			
			m_LoginIdTextBox.Text = user.Login;
			m_LoginPassTextBox.Text = user.Password;

			// if background login requested
			if( state == UserLoginState.Silent )
			{
				m_RequestLoginState = state;
				ShowErrors = false; // fail silently
				
				StartPosition = FormStartPosition.Manual;
				Location = new Point( -32000, -32000 );

				m_CancelButton.Enabled = false;
				ShowInTaskbar = false;
			}
		}

		private void EnableInterface( )
		{
			m_RegisterGroupBox.Enabled = true;
			m_LoginGroupBox.Enabled = true;
			m_AcceptButton.Visible = true;
			m_AcceptButton.Focus();
		}

		private void DisableInterface( )
		{
			m_AcceptButton.Visible = false;
			m_LoginGroupBox.Enabled = false;
			m_RegisterGroupBox.Enabled = false;
		}

		private void m_NewToBuzmLinkLabel_LinkClicked( object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e )
		{
			this.Text = REGISTER_FORM_TITLE;
			m_AcceptButton.Text = REGISTER_BUTTON_TITLE;
			Action = RegistryAction.InsertUser; 

			m_PrivacyLinkLabel.Visible = true;
			m_RegisterPanel.BringToFront();
		}

		private void m_PrivacyLinkLabel_LinkClicked( object sender, LinkLabelLinkClickedEventArgs e )
		{
			// open privacy policy in browser window
			HiveView.OpenWithNewBrowser( PRIVACY_URL );
		}

		private void m_AcceptButton_Click( object sender, System.EventArgs e )
		{			
			DisableInterface(); // prevent user resubmitting
			if( ProcessAction( Action ) ) BeginRegistryRequest();
		}

		private void m_CancelButton_Click(object sender, System.EventArgs e)
		{
			this.Close(); // hide the registration window from view
		}

		protected override bool ShowWithoutActivation
		{
			get { return ( m_RequestLoginState == UserLoginState.Silent ); }
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UserEditor));
			this.m_LogoPictureBox = new System.Windows.Forms.PictureBox();
			this.m_LoginPanel = new System.Windows.Forms.Panel();
			this.m_LoginGroupBox = new System.Windows.Forms.GroupBox();
			this.m_NewToBuzmLinkLabel = new System.Windows.Forms.LinkLabel();
			this.m_ForgotPassLinkLabel = new System.Windows.Forms.LinkLabel();
			this.m_ForgotPassLabel = new System.Windows.Forms.Label();
			this.m_LoginRemCheckBox = new System.Windows.Forms.CheckBox();
			this.m_LoginPassTextBox = new System.Windows.Forms.TextBox();
			this.m_LoginPassLabel = new System.Windows.Forms.Label();
			this.m_LoginIdTextBox = new System.Windows.Forms.TextBox();
			this.m_LoginIdLabel = new System.Windows.Forms.Label();
			this.m_RegisterPanel = new System.Windows.Forms.Panel();
			this.m_RegisterGroupBox = new System.Windows.Forms.GroupBox();
			this.m_RegisterEmailTextBox = new System.Windows.Forms.TextBox();
			this.m_RegisterEmailLabel = new System.Windows.Forms.Label();
			this.m_RegisterPassAgainTextBox = new System.Windows.Forms.TextBox();
			this.m_RegisterPassAgainLabel = new System.Windows.Forms.Label();
			this.m_RegisterPassTextBox = new System.Windows.Forms.TextBox();
			this.m_RegisterPassLabel = new System.Windows.Forms.Label();
			this.m_RegisterIdTextBox = new System.Windows.Forms.TextBox();
			this.m_RegisterIdLabel = new System.Windows.Forms.Label();
			this.m_AcceptButton = new System.Windows.Forms.Button();
			this.m_CancelButton = new System.Windows.Forms.Button();
			this.m_LoadingLabel = new System.Windows.Forms.Label();
			this.m_PrivacyLinkLabel = new System.Windows.Forms.LinkLabel();
			((System.ComponentModel.ISupportInitialize)(this.m_LogoPictureBox)).BeginInit();
			this.m_LoginPanel.SuspendLayout();
			this.m_LoginGroupBox.SuspendLayout();
			this.m_RegisterPanel.SuspendLayout();
			this.m_RegisterGroupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// m_ActionProgressBar
			// 
			this.m_ActionProgressBar.Location = new System.Drawing.Point(176, 184);
			this.m_ActionProgressBar.Size = new System.Drawing.Size(200, 23);
			// 
			// m_LogoPictureBox
			// 
			this.m_LogoPictureBox.Dock = System.Windows.Forms.DockStyle.Left;
			this.m_LogoPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_LogoPictureBox.Image")));
			this.m_LogoPictureBox.Location = new System.Drawing.Point(0, 0);
			this.m_LogoPictureBox.Name = "m_LogoPictureBox";
			this.m_LogoPictureBox.Size = new System.Drawing.Size(163, 214);
			this.m_LogoPictureBox.TabIndex = 0;
			this.m_LogoPictureBox.TabStop = false;
			// 
			// m_LoginPanel
			// 
			this.m_LoginPanel.Controls.Add(this.m_LoginGroupBox);
			this.m_LoginPanel.Location = new System.Drawing.Point(168, 0);
			this.m_LoginPanel.Name = "m_LoginPanel";
			this.m_LoginPanel.Size = new System.Drawing.Size(312, 176);
			this.m_LoginPanel.TabIndex = 1;
			// 
			// m_LoginGroupBox
			// 
			this.m_LoginGroupBox.Controls.Add(this.m_NewToBuzmLinkLabel);
			this.m_LoginGroupBox.Controls.Add(this.m_ForgotPassLinkLabel);
			this.m_LoginGroupBox.Controls.Add(this.m_ForgotPassLabel);
			this.m_LoginGroupBox.Controls.Add(this.m_LoginRemCheckBox);
			this.m_LoginGroupBox.Controls.Add(this.m_LoginPassTextBox);
			this.m_LoginGroupBox.Controls.Add(this.m_LoginPassLabel);
			this.m_LoginGroupBox.Controls.Add(this.m_LoginIdTextBox);
			this.m_LoginGroupBox.Controls.Add(this.m_LoginIdLabel);
			this.m_LoginGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_LoginGroupBox.Location = new System.Drawing.Point(8, 8);
			this.m_LoginGroupBox.Name = "m_LoginGroupBox";
			this.m_LoginGroupBox.Size = new System.Drawing.Size(288, 168);
			this.m_LoginGroupBox.TabIndex = 0;
			this.m_LoginGroupBox.TabStop = false;
			this.m_LoginGroupBox.Text = "Welcome to Buzm, please login to continue:";
			// 
			// m_NewToBuzmLinkLabel
			// 
			this.m_NewToBuzmLinkLabel.AutoSize = true;
			this.m_NewToBuzmLinkLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_NewToBuzmLinkLabel.LinkArea = new System.Windows.Forms.LinkArea(14, 18);
			this.m_NewToBuzmLinkLabel.Location = new System.Drawing.Point(8, 144);
			this.m_NewToBuzmLinkLabel.Name = "m_NewToBuzmLinkLabel";
			this.m_NewToBuzmLinkLabel.Size = new System.Drawing.Size(273, 18);
			this.m_NewToBuzmLinkLabel.TabIndex = 8;
			this.m_NewToBuzmLinkLabel.TabStop = true;
			this.m_NewToBuzmLinkLabel.Text = "New to Buzm?  Click here to Join - It\'s Free and Easy!";
			this.m_NewToBuzmLinkLabel.UseCompatibleTextRendering = true;
			this.m_NewToBuzmLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.m_NewToBuzmLinkLabel_LinkClicked);
			// 
			// m_ForgotPassLinkLabel
			// 
			this.m_ForgotPassLinkLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_ForgotPassLinkLabel.Location = new System.Drawing.Point(128, 128);
			this.m_ForgotPassLinkLabel.Name = "m_ForgotPassLinkLabel";
			this.m_ForgotPassLinkLabel.Size = new System.Drawing.Size(144, 16);
			this.m_ForgotPassLinkLabel.TabIndex = 7;
			this.m_ForgotPassLinkLabel.TabStop = true;
			this.m_ForgotPassLinkLabel.Text = "Click here to get a reminder";
			this.m_ForgotPassLinkLabel.Visible = false;
			// 
			// m_ForgotPassLabel
			// 
			this.m_ForgotPassLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_ForgotPassLabel.Location = new System.Drawing.Point(8, 128);
			this.m_ForgotPassLabel.Name = "m_ForgotPassLabel";
			this.m_ForgotPassLabel.Size = new System.Drawing.Size(120, 16);
			this.m_ForgotPassLabel.TabIndex = 4;
			this.m_ForgotPassLabel.Text = "Forgot your password? ";
			this.m_ForgotPassLabel.Visible = false;
			// 
			// m_LoginRemCheckBox
			// 
			this.m_LoginRemCheckBox.Enabled = false;
			this.m_LoginRemCheckBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_LoginRemCheckBox.Location = new System.Drawing.Point(72, 96);
			this.m_LoginRemCheckBox.Name = "m_LoginRemCheckBox";
			this.m_LoginRemCheckBox.Size = new System.Drawing.Size(212, 16);
			this.m_LoginRemCheckBox.TabIndex = 3;
			this.m_LoginRemCheckBox.Text = "Remember my login on this computer";
			this.m_LoginRemCheckBox.Visible = false;
			// 
			// m_LoginPassTextBox
			// 
			this.m_LoginPassTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_LoginPassTextBox.Location = new System.Drawing.Point(72, 64);
			this.m_LoginPassTextBox.MaxLength = 15;
			this.m_LoginPassTextBox.Name = "m_LoginPassTextBox";
			this.m_LoginPassTextBox.PasswordChar = '*';
			this.m_LoginPassTextBox.Size = new System.Drawing.Size(200, 21);
			this.m_LoginPassTextBox.TabIndex = 2;
			// 
			// m_LoginPassLabel
			// 
			this.m_LoginPassLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_LoginPassLabel.Location = new System.Drawing.Point(8, 64);
			this.m_LoginPassLabel.Name = "m_LoginPassLabel";
			this.m_LoginPassLabel.Size = new System.Drawing.Size(56, 23);
			this.m_LoginPassLabel.TabIndex = 3;
			this.m_LoginPassLabel.Text = "Password:";
			this.m_LoginPassLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_LoginIdTextBox
			// 
			this.m_LoginIdTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_LoginIdTextBox.Location = new System.Drawing.Point(72, 32);
			this.m_LoginIdTextBox.MaxLength = 50;
			this.m_LoginIdTextBox.Name = "m_LoginIdTextBox";
			this.m_LoginIdTextBox.Size = new System.Drawing.Size(200, 21);
			this.m_LoginIdTextBox.TabIndex = 1;
			// 
			// m_LoginIdLabel
			// 
			this.m_LoginIdLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_LoginIdLabel.Location = new System.Drawing.Point(8, 32);
			this.m_LoginIdLabel.Name = "m_LoginIdLabel";
			this.m_LoginIdLabel.Size = new System.Drawing.Size(64, 23);
			this.m_LoginIdLabel.TabIndex = 2;
			this.m_LoginIdLabel.Text = "Buzm ID:";
			this.m_LoginIdLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_RegisterPanel
			// 
			this.m_RegisterPanel.Controls.Add(this.m_RegisterGroupBox);
			this.m_RegisterPanel.Location = new System.Drawing.Point(168, 0);
			this.m_RegisterPanel.Name = "m_RegisterPanel";
			this.m_RegisterPanel.Size = new System.Drawing.Size(312, 176);
			this.m_RegisterPanel.TabIndex = 2;
			// 
			// m_RegisterGroupBox
			// 
			this.m_RegisterGroupBox.Controls.Add(this.m_RegisterEmailTextBox);
			this.m_RegisterGroupBox.Controls.Add(this.m_RegisterEmailLabel);
			this.m_RegisterGroupBox.Controls.Add(this.m_RegisterPassAgainTextBox);
			this.m_RegisterGroupBox.Controls.Add(this.m_RegisterPassAgainLabel);
			this.m_RegisterGroupBox.Controls.Add(this.m_RegisterPassTextBox);
			this.m_RegisterGroupBox.Controls.Add(this.m_RegisterPassLabel);
			this.m_RegisterGroupBox.Controls.Add(this.m_RegisterIdTextBox);
			this.m_RegisterGroupBox.Controls.Add(this.m_RegisterIdLabel);
			this.m_RegisterGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_RegisterGroupBox.Location = new System.Drawing.Point(8, 8);
			this.m_RegisterGroupBox.Name = "m_RegisterGroupBox";
			this.m_RegisterGroupBox.Size = new System.Drawing.Size(288, 168);
			this.m_RegisterGroupBox.TabIndex = 0;
			this.m_RegisterGroupBox.TabStop = false;
			this.m_RegisterGroupBox.Text = "Create a free Buzm account:";
			// 
			// m_RegisterEmailTextBox
			// 
			this.m_RegisterEmailTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_RegisterEmailTextBox.Location = new System.Drawing.Point(96, 128);
			this.m_RegisterEmailTextBox.MaxLength = 255;
			this.m_RegisterEmailTextBox.Name = "m_RegisterEmailTextBox";
			this.m_RegisterEmailTextBox.Size = new System.Drawing.Size(176, 21);
			this.m_RegisterEmailTextBox.TabIndex = 4;
			// 
			// m_RegisterEmailLabel
			// 
			this.m_RegisterEmailLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_RegisterEmailLabel.Location = new System.Drawing.Point(8, 128);
			this.m_RegisterEmailLabel.Name = "m_RegisterEmailLabel";
			this.m_RegisterEmailLabel.Size = new System.Drawing.Size(84, 23);
			this.m_RegisterEmailLabel.TabIndex = 4;
			this.m_RegisterEmailLabel.Text = "Email Address:";
			this.m_RegisterEmailLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_RegisterPassAgainTextBox
			// 
			this.m_RegisterPassAgainTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_RegisterPassAgainTextBox.Location = new System.Drawing.Point(96, 96);
			this.m_RegisterPassAgainTextBox.MaxLength = 15;
			this.m_RegisterPassAgainTextBox.Name = "m_RegisterPassAgainTextBox";
			this.m_RegisterPassAgainTextBox.PasswordChar = '*';
			this.m_RegisterPassAgainTextBox.Size = new System.Drawing.Size(176, 21);
			this.m_RegisterPassAgainTextBox.TabIndex = 3;
			// 
			// m_RegisterPassAgainLabel
			// 
			this.m_RegisterPassAgainLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_RegisterPassAgainLabel.Location = new System.Drawing.Point(8, 96);
			this.m_RegisterPassAgainLabel.Name = "m_RegisterPassAgainLabel";
			this.m_RegisterPassAgainLabel.Size = new System.Drawing.Size(96, 23);
			this.m_RegisterPassAgainLabel.TabIndex = 3;
			this.m_RegisterPassAgainLabel.Text = "Password Again:";
			this.m_RegisterPassAgainLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_RegisterPassTextBox
			// 
			this.m_RegisterPassTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_RegisterPassTextBox.Location = new System.Drawing.Point(96, 64);
			this.m_RegisterPassTextBox.MaxLength = 15;
			this.m_RegisterPassTextBox.Name = "m_RegisterPassTextBox";
			this.m_RegisterPassTextBox.PasswordChar = '*';
			this.m_RegisterPassTextBox.Size = new System.Drawing.Size(176, 21);
			this.m_RegisterPassTextBox.TabIndex = 2;
			// 
			// m_RegisterPassLabel
			// 
			this.m_RegisterPassLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_RegisterPassLabel.Location = new System.Drawing.Point(8, 64);
			this.m_RegisterPassLabel.Name = "m_RegisterPassLabel";
			this.m_RegisterPassLabel.Size = new System.Drawing.Size(56, 23);
			this.m_RegisterPassLabel.TabIndex = 2;
			this.m_RegisterPassLabel.Text = "Password:";
			this.m_RegisterPassLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_RegisterIdTextBox
			// 
			this.m_RegisterIdTextBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_RegisterIdTextBox.Location = new System.Drawing.Point(96, 32);
			this.m_RegisterIdTextBox.MaxLength = 50;
			this.m_RegisterIdTextBox.Name = "m_RegisterIdTextBox";
			this.m_RegisterIdTextBox.Size = new System.Drawing.Size(176, 21);
			this.m_RegisterIdTextBox.TabIndex = 1;
			// 
			// m_RegisterIdLabel
			// 
			this.m_RegisterIdLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_RegisterIdLabel.Location = new System.Drawing.Point(8, 32);
			this.m_RegisterIdLabel.Name = "m_RegisterIdLabel";
			this.m_RegisterIdLabel.Size = new System.Drawing.Size(64, 23);
			this.m_RegisterIdLabel.TabIndex = 0;
			this.m_RegisterIdLabel.Text = "Buzm ID:";
			this.m_RegisterIdLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_AcceptButton
			// 
			this.m_AcceptButton.Location = new System.Drawing.Point(304, 184);
			this.m_AcceptButton.Name = "m_AcceptButton";
			this.m_AcceptButton.Size = new System.Drawing.Size(75, 23);
			this.m_AcceptButton.TabIndex = 3;
			this.m_AcceptButton.Text = "Login";
			this.m_AcceptButton.Click += new System.EventHandler(this.m_AcceptButton_Click);
			// 
			// m_CancelButton
			// 
			this.m_CancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.m_CancelButton.Location = new System.Drawing.Point(389, 184);
			this.m_CancelButton.Name = "m_CancelButton";
			this.m_CancelButton.Size = new System.Drawing.Size(75, 23);
			this.m_CancelButton.TabIndex = 4;
			this.m_CancelButton.Text = "Cancel";
			this.m_CancelButton.Click += new System.EventHandler(this.m_CancelButton_Click);
			// 
			// m_LoadingLabel
			// 
			this.m_LoadingLabel.Location = new System.Drawing.Point(176, 184);
			this.m_LoadingLabel.Name = "m_LoadingLabel";
			this.m_LoadingLabel.Size = new System.Drawing.Size(200, 23);
			this.m_LoadingLabel.TabIndex = 6;
			this.m_LoadingLabel.Text = "Loading Hives for ";
			this.m_LoadingLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.m_LoadingLabel.Visible = false;
			// 
			// m_PrivacyLinkLabel
			// 
			this.m_PrivacyLinkLabel.Location = new System.Drawing.Point(184, 184);
			this.m_PrivacyLinkLabel.Name = "m_PrivacyLinkLabel";
			this.m_PrivacyLinkLabel.Size = new System.Drawing.Size(100, 23);
			this.m_PrivacyLinkLabel.TabIndex = 7;
			this.m_PrivacyLinkLabel.TabStop = true;
			this.m_PrivacyLinkLabel.Text = "Privacy Policy";
			this.m_PrivacyLinkLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.m_PrivacyLinkLabel.Visible = false;
			this.m_PrivacyLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.m_PrivacyLinkLabel_LinkClicked);
			// 
			// UserEditor
			// 
			this.AcceptButton = this.m_AcceptButton;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 14);
			this.CancelButton = this.m_CancelButton;
			this.ClientSize = new System.Drawing.Size(475, 214);
			this.Controls.Add(this.m_CancelButton);
			this.Controls.Add(this.m_AcceptButton);
			this.Controls.Add(this.m_LogoPictureBox);
			this.Controls.Add(this.m_LoginPanel);
			this.Controls.Add(this.m_RegisterPanel);
			this.Controls.Add(this.m_LoadingLabel);
			this.Controls.Add(this.m_PrivacyLinkLabel);
			this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.Name = "UserEditor";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Network Login - Buzm";
			this.Controls.SetChildIndex(this.m_PrivacyLinkLabel, 0);
			this.Controls.SetChildIndex(this.m_LoadingLabel, 0);
			this.Controls.SetChildIndex(this.m_RegisterPanel, 0);
			this.Controls.SetChildIndex(this.m_LoginPanel, 0);
			this.Controls.SetChildIndex(this.m_LogoPictureBox, 0);
			this.Controls.SetChildIndex(this.m_AcceptButton, 0);
			this.Controls.SetChildIndex(this.m_CancelButton, 0);
			this.Controls.SetChildIndex(this.m_ActionProgressBar, 0);
			((System.ComponentModel.ISupportInitialize)(this.m_LogoPictureBox)).EndInit();
			this.m_LoginPanel.ResumeLayout(false);
			this.m_LoginGroupBox.ResumeLayout(false);
			this.m_LoginGroupBox.PerformLayout();
			this.m_RegisterPanel.ResumeLayout(false);
			this.m_RegisterGroupBox.ResumeLayout(false);
			this.m_RegisterGroupBox.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		/// <summary> Clean up any resources </summary>
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
	}
}
