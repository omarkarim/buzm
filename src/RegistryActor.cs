using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace Buzm
{
	/// <summary>Base class for Registry actions that 
	/// require no user input to be initiated.</summary>
	public class RegistryActor : RegistryEditor
	{
		private Label m_ProgressLabel;
		private Button m_CancelButton;
		private IContainer components = null;

		public RegistryActor()
		{
			InitializeComponent(); // forms designer code
		}

		private void RegistryActor_Load( object sender, EventArgs e )
		{
			if( !DesignMode ) // if event not fired from designer
			{ 
				// if setup succeeds execute action asynchronously
				if( SetupRegistryRequest() ) BeginRegistryRequest(); 
				else Close(); // hide and cleanup form resources
			}
		}
		
		/// <summary>This method should be overriden by child
		/// classes to configure the registry action </summary>
		/// <returns>True if the setup was successful </returns>
		protected virtual bool SetupRegistryRequest( ){ return false; }

		private void m_CancelButton_Click( object sender, EventArgs e )
		{
			Close(); // call overridden method to cleanup resources
		}

		public string ActionText
		{ 
			get { return m_ProgressLabel.Text; } 
			set { m_ProgressLabel.Text = value; }
		}

		/// <summary>Overridden to cleanup resources as closing
		/// event is not called from the form's load event. Also,
		/// modal dialogs will hold memory until Disposed</summary>
		public new void Close() 
		{
			ClearRegistryEvents();
			base.Close();
			Dispose();
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
			this.components = new System.ComponentModel.Container();
			this.m_CancelButton = new System.Windows.Forms.Button();
			this.m_ProgressLabel = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// m_ActionProgressBar
			// 
			this.m_ActionProgressBar.Location = new System.Drawing.Point(8, 24);
			this.m_ActionProgressBar.Name = "m_ActionProgressBar";
			this.m_ActionProgressBar.Visible = true;
			// 
			// m_CancelButton
			// 
			this.m_CancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.m_CancelButton.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.m_CancelButton.Location = new System.Drawing.Point(280, 24);
			this.m_CancelButton.Name = "m_CancelButton";
			this.m_CancelButton.Size = new System.Drawing.Size(100, 25);
			this.m_CancelButton.TabIndex = 1;
			this.m_CancelButton.Text = "Cancel";
			this.m_CancelButton.Click += new System.EventHandler(this.m_CancelButton_Click);
			// 
			// m_ProgressLabel
			// 
			this.m_ProgressLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.m_ProgressLabel.Location = new System.Drawing.Point(8, 0);
			this.m_ProgressLabel.Name = "m_ProgressLabel";
			this.m_ProgressLabel.Size = new System.Drawing.Size(376, 23);
			this.m_ProgressLabel.TabIndex = 2;
			this.m_ProgressLabel.Text = "Enter text describing the action here...";
			this.m_ProgressLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// RegistryActor
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 14);
			this.CancelButton = this.m_CancelButton;
			this.ClientSize = new System.Drawing.Size(386, 58);
			this.Controls.Add(this.m_ProgressLabel);
			this.Controls.Add(this.m_CancelButton);
			this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			this.MaximizeBox = false;
			this.Name = "RegistryActor";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Load += new System.EventHandler(this.RegistryActor_Load);
			this.Controls.SetChildIndex(this.m_CancelButton, 0);
			this.Controls.SetChildIndex(this.m_ProgressLabel, 0);
			this.Controls.SetChildIndex(this.m_ActionProgressBar, 0);
			this.ResumeLayout(false);

		}
		#endregion

	}
}

