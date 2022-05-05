using Buzm.Utility;

namespace Buzm
{
	/// <summary>About Buzm menu window</summary>
	public class About : System.Windows.Forms.Form
	{
		private AppVersion m_AppVersion; // buzm version
		private System.Windows.Forms.Button m_AcceptButton;
		private System.Windows.Forms.GroupBox m_InfoGroupBox;
		private System.Windows.Forms.Label m_CopyrightLabel;
		private System.Windows.Forms.Label m_VersionLabel;
		private System.Windows.Forms.Label m_ReservedLabel;
		private System.Windows.Forms.Label m_HomeUrlLabel;
		private System.Windows.Forms.Label m_HelpUrlLabel;
		private System.Windows.Forms.Label m_DownloadUrlLabel;
		private System.Windows.Forms.PictureBox m_LogoPictureBox;
		private System.ComponentModel.Container components = null;

		public About( AppVersion version )
		{
			InitializeComponent(); // designer code
			m_AppVersion = version; // save version info
			m_VersionLabel.Text += m_AppVersion.ToString();
		}

		private void m_AcceptButton_Click( object sender, System.EventArgs e )
		{
			Close(); // close modal dialog
			Dispose(); // release form memory
		}

		/// <summary>Clean up resources</summary>
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(About));
			this.m_LogoPictureBox = new System.Windows.Forms.PictureBox();
			this.m_InfoGroupBox = new System.Windows.Forms.GroupBox();
			this.m_DownloadUrlLabel = new System.Windows.Forms.Label();
			this.m_HelpUrlLabel = new System.Windows.Forms.Label();
			this.m_HomeUrlLabel = new System.Windows.Forms.Label();
			this.m_ReservedLabel = new System.Windows.Forms.Label();
			this.m_CopyrightLabel = new System.Windows.Forms.Label();
			this.m_VersionLabel = new System.Windows.Forms.Label();
			this.m_AcceptButton = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.m_LogoPictureBox)).BeginInit();
			this.m_InfoGroupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// m_LogoPictureBox
			// 
			this.m_LogoPictureBox.Dock = System.Windows.Forms.DockStyle.Left;
			this.m_LogoPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_LogoPictureBox.Image")));
			this.m_LogoPictureBox.Location = new System.Drawing.Point(0, 0);
			this.m_LogoPictureBox.Name = "m_LogoPictureBox";
			this.m_LogoPictureBox.Size = new System.Drawing.Size(163, 214);
			this.m_LogoPictureBox.TabIndex = 1;
			this.m_LogoPictureBox.TabStop = false;
			// 
			// m_InfoGroupBox
			// 
			this.m_InfoGroupBox.Controls.Add(this.m_DownloadUrlLabel);
			this.m_InfoGroupBox.Controls.Add(this.m_HelpUrlLabel);
			this.m_InfoGroupBox.Controls.Add(this.m_HomeUrlLabel);
			this.m_InfoGroupBox.Controls.Add(this.m_ReservedLabel);
			this.m_InfoGroupBox.Controls.Add(this.m_CopyrightLabel);
			this.m_InfoGroupBox.Controls.Add(this.m_VersionLabel);
			this.m_InfoGroupBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_InfoGroupBox.Location = new System.Drawing.Point(176, 8);
			this.m_InfoGroupBox.Name = "m_InfoGroupBox";
			this.m_InfoGroupBox.Size = new System.Drawing.Size(245, 168);
			this.m_InfoGroupBox.TabIndex = 2;
			this.m_InfoGroupBox.TabStop = false;
			this.m_InfoGroupBox.Text = "Buzm Beta";
			// 
			// m_DownloadUrlLabel
			// 
			this.m_DownloadUrlLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_DownloadUrlLabel.Location = new System.Drawing.Point(16, 140);
			this.m_DownloadUrlLabel.Name = "m_DownloadUrlLabel";
			this.m_DownloadUrlLabel.Size = new System.Drawing.Size(152, 23);
			this.m_DownloadUrlLabel.TabIndex = 5;
			this.m_DownloadUrlLabel.Text = "www.buzm.com/download";
			// 
			// m_HelpUrlLabel
			// 
			this.m_HelpUrlLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_HelpUrlLabel.Location = new System.Drawing.Point(16, 120);
			this.m_HelpUrlLabel.Name = "m_HelpUrlLabel";
			this.m_HelpUrlLabel.Size = new System.Drawing.Size(136, 23);
			this.m_HelpUrlLabel.TabIndex = 4;
			this.m_HelpUrlLabel.Text = "www.buzm.com/help";
			// 
			// m_HomeUrlLabel
			// 
			this.m_HomeUrlLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_HomeUrlLabel.Location = new System.Drawing.Point(16, 100);
			this.m_HomeUrlLabel.Name = "m_HomeUrlLabel";
			this.m_HomeUrlLabel.Size = new System.Drawing.Size(100, 23);
			this.m_HomeUrlLabel.TabIndex = 3;
			this.m_HomeUrlLabel.Text = "www.buzm.com";
			// 
			// m_ReservedLabel
			// 
			this.m_ReservedLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_ReservedLabel.Location = new System.Drawing.Point(16, 68);
			this.m_ReservedLabel.Name = "m_ReservedLabel";
			this.m_ReservedLabel.Size = new System.Drawing.Size(144, 23);
			this.m_ReservedLabel.TabIndex = 2;
			this.m_ReservedLabel.Text = "All Rights Reserved.";
			// 
			// m_CopyrightLabel
			// 
			this.m_CopyrightLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_CopyrightLabel.Location = new System.Drawing.Point(16, 48);
			this.m_CopyrightLabel.Name = "m_CopyrightLabel";
			this.m_CopyrightLabel.Size = new System.Drawing.Size(168, 23);
			this.m_CopyrightLabel.TabIndex = 1;
			this.m_CopyrightLabel.Text = "Copyright © 2007 Buzm LLC";
			// 
			// m_VersionLabel
			// 
			this.m_VersionLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_VersionLabel.Location = new System.Drawing.Point(16, 28);
			this.m_VersionLabel.Name = "m_VersionLabel";
			this.m_VersionLabel.Size = new System.Drawing.Size(192, 23);
			this.m_VersionLabel.TabIndex = 0;
			this.m_VersionLabel.Text = "Version: ";
			// 
			// m_AcceptButton
			// 
			this.m_AcceptButton.Location = new System.Drawing.Point(346, 184);
			this.m_AcceptButton.Name = "m_AcceptButton";
			this.m_AcceptButton.Size = new System.Drawing.Size(75, 23);
			this.m_AcceptButton.TabIndex = 3;
			this.m_AcceptButton.Text = "OK";
			this.m_AcceptButton.Click += new System.EventHandler(this.m_AcceptButton_Click);
			// 
			// About
			// 
			this.AcceptButton = this.m_AcceptButton;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(432, 214);
			this.Controls.Add(this.m_AcceptButton);
			this.Controls.Add(this.m_InfoGroupBox);
			this.Controls.Add(this.m_LogoPictureBox);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.Name = "About";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "About Buzm";
			((System.ComponentModel.ISupportInitialize)(this.m_LogoPictureBox)).EndInit();
			this.m_InfoGroupBox.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion
	}
}
