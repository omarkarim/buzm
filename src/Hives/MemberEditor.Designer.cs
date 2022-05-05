namespace Buzm.Hives
{
	partial class MemberEditor
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
			{
				components.Dispose();
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( MemberEditor ) );
			this.m_InviteGroupBox = new System.Windows.Forms.GroupBox();
			this.m_InviteMessageSubLabel = new System.Windows.Forms.Label();
			this.m_InviteMessageLabel = new System.Windows.Forms.Label();
			this.m_InviteEmailLabel = new System.Windows.Forms.Label();
			this.m_InviteMessageTextBox = new System.Windows.Forms.TextBox();
			this.m_InviteEmailTextBox = new System.Windows.Forms.TextBox();
			this.m_CancelButton = new System.Windows.Forms.Button();
			this.m_AcceptButton = new System.Windows.Forms.Button();
			this.m_HiveLabel = new System.Windows.Forms.Label();
			this.m_HiveComboBox = new System.Windows.Forms.ComboBox();
			this.m_InviteGroupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// m_ActionProgressBar
			// 
			this.m_ActionProgressBar.Location = new System.Drawing.Point( 8, 301 );
			this.m_ActionProgressBar.Size = new System.Drawing.Size( 265, 21 );
			// 
			// m_InviteGroupBox
			// 
			this.m_InviteGroupBox.Controls.Add( this.m_InviteMessageSubLabel );
			this.m_InviteGroupBox.Controls.Add( this.m_InviteMessageLabel );
			this.m_InviteGroupBox.Controls.Add( this.m_InviteEmailLabel );
			this.m_InviteGroupBox.Controls.Add( this.m_InviteMessageTextBox );
			this.m_InviteGroupBox.Controls.Add( this.m_InviteEmailTextBox );
			this.m_InviteGroupBox.Location = new System.Drawing.Point( 8, 37 );
			this.m_InviteGroupBox.Name = "m_InviteGroupBox";
			this.m_InviteGroupBox.Size = new System.Drawing.Size( 378, 257 );
			this.m_InviteGroupBox.TabIndex = 5;
			this.m_InviteGroupBox.TabStop = false;
			this.m_InviteGroupBox.Text = "Invite your Friends";
			// 
			// m_InviteMessageSubLabel
			// 
			this.m_InviteMessageSubLabel.AutoSize = true;
			this.m_InviteMessageSubLabel.Font = new System.Drawing.Font( "Tahoma", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte)( 0 ) ) );
			this.m_InviteMessageSubLabel.Location = new System.Drawing.Point( 16, 123 );
			this.m_InviteMessageSubLabel.Name = "m_InviteMessageSubLabel";
			this.m_InviteMessageSubLabel.Size = new System.Drawing.Size( 326, 11 );
			this.m_InviteMessageSubLabel.TabIndex = 5;
			this.m_InviteMessageSubLabel.Text = "(We will add some instructional text so your friends know how to setup Buzm)";
			this.m_InviteMessageSubLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_InviteMessageLabel
			// 
			this.m_InviteMessageLabel.AutoSize = true;
			this.m_InviteMessageLabel.Location = new System.Drawing.Point( 16, 107 );
			this.m_InviteMessageLabel.Name = "m_InviteMessageLabel";
			this.m_InviteMessageLabel.Size = new System.Drawing.Size( 194, 13 );
			this.m_InviteMessageLabel.TabIndex = 3;
			this.m_InviteMessageLabel.Text = "Add a personal message or leave as is:";
			this.m_InviteMessageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_InviteEmailLabel
			// 
			this.m_InviteEmailLabel.AutoSize = true;
			this.m_InviteEmailLabel.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte)( 0 ) ) );
			this.m_InviteEmailLabel.Location = new System.Drawing.Point( 16, 22 );
			this.m_InviteEmailLabel.Name = "m_InviteEmailLabel";
			this.m_InviteEmailLabel.Size = new System.Drawing.Size( 249, 13 );
			this.m_InviteEmailLabel.TabIndex = 2;
			this.m_InviteEmailLabel.Text = "Enter their email addresses separated by commas:";
			this.m_InviteEmailLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_InviteMessageTextBox
			// 
			this.m_InviteMessageTextBox.AcceptsReturn = true;
			this.m_InviteMessageTextBox.Location = new System.Drawing.Point( 16, 139 );
			this.m_InviteMessageTextBox.MaxLength = 5000;
			this.m_InviteMessageTextBox.Multiline = true;
			this.m_InviteMessageTextBox.Name = "m_InviteMessageTextBox";
			this.m_InviteMessageTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.m_InviteMessageTextBox.Size = new System.Drawing.Size( 346, 102 );
			this.m_InviteMessageTextBox.TabIndex = 2;
			// 
			// m_InviteEmailTextBox
			// 
			this.m_InviteEmailTextBox.AcceptsReturn = true;
			this.m_InviteEmailTextBox.Location = new System.Drawing.Point( 16, 40 );
			this.m_InviteEmailTextBox.MaxLength = 1000;
			this.m_InviteEmailTextBox.Multiline = true;
			this.m_InviteEmailTextBox.Name = "m_InviteEmailTextBox";
			this.m_InviteEmailTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.m_InviteEmailTextBox.Size = new System.Drawing.Size( 346, 54 );
			this.m_InviteEmailTextBox.TabIndex = 1;
			// 
			// m_CancelButton
			// 
			this.m_CancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.m_CancelButton.Location = new System.Drawing.Point( 286, 301 );
			this.m_CancelButton.Name = "m_CancelButton";
			this.m_CancelButton.Size = new System.Drawing.Size( 100, 21 );
			this.m_CancelButton.TabIndex = 4;
			this.m_CancelButton.Text = "Cancel";
			this.m_CancelButton.Click += new System.EventHandler( this.m_CancelButton_Click );
			// 
			// m_AcceptButton
			// 
			this.m_AcceptButton.Location = new System.Drawing.Point( 178, 301 );
			this.m_AcceptButton.Name = "m_AcceptButton";
			this.m_AcceptButton.Size = new System.Drawing.Size( 100, 21 );
			this.m_AcceptButton.TabIndex = 3;
			this.m_AcceptButton.Text = "Send Invite";
			this.m_AcceptButton.Click += new System.EventHandler( this.m_AcceptButton_Click );
			// 
			// m_HiveLabel
			// 
			this.m_HiveLabel.Font = new System.Drawing.Font( "Tahoma", 8.25F );
			this.m_HiveLabel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
			this.m_HiveLabel.Location = new System.Drawing.Point( 10, 7 );
			this.m_HiveLabel.Name = "m_HiveLabel";
			this.m_HiveLabel.Size = new System.Drawing.Size( 38, 21 );
			this.m_HiveLabel.TabIndex = 12;
			this.m_HiveLabel.Text = "Hive:";
			this.m_HiveLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_HiveComboBox
			// 
			this.m_HiveComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.m_HiveComboBox.Font = new System.Drawing.Font( "Tahoma", 8.25F );
			this.m_HiveComboBox.ItemHeight = 13;
			this.m_HiveComboBox.Location = new System.Drawing.Point( 56, 8 );
			this.m_HiveComboBox.Name = "m_HiveComboBox";
			this.m_HiveComboBox.Size = new System.Drawing.Size( 330, 21 );
			this.m_HiveComboBox.Sorted = true;
			this.m_HiveComboBox.TabIndex = 5;
			// 
			// MemberEditor
			// 
			this.AcceptButton = this.m_AcceptButton;
			this.CancelButton = this.m_CancelButton;
			this.ClientSize = new System.Drawing.Size( 394, 328 );
			this.Controls.Add( this.m_HiveComboBox );
			this.Controls.Add( this.m_HiveLabel );
			this.Controls.Add( this.m_AcceptButton );
			this.Controls.Add( this.m_CancelButton );
			this.Controls.Add( this.m_InviteGroupBox );
			this.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte)( 0 ) ) );
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Icon = ( (System.Drawing.Icon)( resources.GetObject( "$this.Icon" ) ) );
			this.MaximizeBox = false;
			this.Name = "MemberEditor";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Send Invite - Buzm";
			this.Controls.SetChildIndex( this.m_ActionProgressBar, 0 );
			this.Controls.SetChildIndex( this.m_InviteGroupBox, 0 );
			this.Controls.SetChildIndex( this.m_CancelButton, 0 );
			this.Controls.SetChildIndex( this.m_AcceptButton, 0 );
			this.Controls.SetChildIndex( this.m_HiveLabel, 0 );
			this.Controls.SetChildIndex( this.m_HiveComboBox, 0 );
			this.m_InviteGroupBox.ResumeLayout( false );
			this.m_InviteGroupBox.PerformLayout();
			this.ResumeLayout( false );

		}

		#endregion

		private System.Windows.Forms.GroupBox m_InviteGroupBox;
		private System.Windows.Forms.Label m_InviteMessageSubLabel;
		private System.Windows.Forms.Label m_InviteMessageLabel;
		private System.Windows.Forms.Label m_InviteEmailLabel;
		private System.Windows.Forms.TextBox m_InviteMessageTextBox;
		private System.Windows.Forms.TextBox m_InviteEmailTextBox;
		private System.Windows.Forms.Button m_CancelButton;
		private System.Windows.Forms.Button m_AcceptButton;
		private System.Windows.Forms.Label m_HiveLabel;
		private System.Windows.Forms.ComboBox m_HiveComboBox;
	}
}
