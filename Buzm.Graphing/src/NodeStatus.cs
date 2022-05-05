using System;
using System.Windows.Forms;

namespace Buzm.Graphing
{
	public class NodeStatus : System.Windows.Forms.UserControl
	{
		private System.Windows.Forms.Label m_StatusHeader;
		private System.Windows.Forms.RichTextBox m_StatusTextBox;

		public NodeStatus( )
		{ 
			// Initialize UI
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.m_StatusHeader = new System.Windows.Forms.Label();
			this.m_StatusTextBox = new System.Windows.Forms.RichTextBox();
			this.SuspendLayout();
			// 
			// m_StatusHeader
			// 
			this.m_StatusHeader.BackColor = System.Drawing.Color.FromArgb(((System.Byte)(255)), ((System.Byte)(102)), ((System.Byte)(0)));
			this.m_StatusHeader.Dock = System.Windows.Forms.DockStyle.Top;
			this.m_StatusHeader.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.m_StatusHeader.Location = new System.Drawing.Point(1, 1);
			this.m_StatusHeader.Name = "m_StatusHeader";
			this.m_StatusHeader.Size = new System.Drawing.Size(148, 13);
			this.m_StatusHeader.TabIndex = 0;
			this.m_StatusHeader.Text = "id:";
			// 
			// m_StatusTextBox
			// 
			this.m_StatusTextBox.BackColor = System.Drawing.SystemColors.ControlDarkDark;
			this.m_StatusTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.m_StatusTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_StatusTextBox.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.m_StatusTextBox.ForeColor = System.Drawing.Color.FromArgb(((System.Byte)(255)), ((System.Byte)(102)), ((System.Byte)(0)));
			this.m_StatusTextBox.Location = new System.Drawing.Point(1, 14);
			this.m_StatusTextBox.Name = "m_StatusTextBox";
			this.m_StatusTextBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
			this.m_StatusTextBox.Size = new System.Drawing.Size(148, 135);
			this.m_StatusTextBox.TabIndex = 1;
			this.m_StatusTextBox.Text = "retreiving node status...";
			// 
			// NodeStatus
			// 
			this.BackColor = System.Drawing.Color.FromArgb(((System.Byte)(255)), ((System.Byte)(102)), ((System.Byte)(0)));
			this.Controls.Add(this.m_StatusTextBox);
			this.Controls.Add(this.m_StatusHeader);
			this.DockPadding.All = 1;
			this.Name = "NodeStatus";
			this.ResumeLayout(false);

		}

		// Expose controls directly for event binding etc
		public Control Header { get { return m_StatusHeader; } }
		public RichTextBox Status { get { return m_StatusTextBox; } }			
	}
}