using System;
using System.Drawing;
using System.Diagnostics;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using Buzm.Utility;
using Buzm.Network.Sockets;

namespace Buzm
{
	/// <summary>
	/// Summary description for Form1.
	/// </summary>
	public class NetworkView : System.Windows.Forms.UserControl 
	{
		private Hashtable m_NodeList;

		private System.Windows.Forms.Splitter splitter1;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.PictureBox pictureBox1;
		private System.Windows.Forms.ColumnHeader columnNode;
		private System.Windows.Forms.ColumnHeader columnUptime;
		private System.Windows.Forms.ColumnHeader columnStatus;
		private System.ComponentModel.IContainer components;
		private System.Windows.Forms.ListView listView1;

		public NetworkView() 
		{
			m_NodeList = new Hashtable();
			InitializeComponent();
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() 
		{
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(NetworkView));
			this.listView1 = new System.Windows.Forms.ListView();
			this.columnNode = new System.Windows.Forms.ColumnHeader();
			this.columnStatus = new System.Windows.Forms.ColumnHeader();
			this.columnUptime = new System.Windows.Forms.ColumnHeader();
			this.splitter1 = new System.Windows.Forms.Splitter();
			this.panel1 = new System.Windows.Forms.Panel();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.panel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// listView1
			// 
			this.listView1.Alignment = System.Windows.Forms.ListViewAlignment.Default;
			this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
																						this.columnNode,
																						this.columnStatus,
																						this.columnUptime});
			this.listView1.Dock = System.Windows.Forms.DockStyle.Top;
			this.listView1.FullRowSelect = true;
			this.listView1.GridLines = true;
			this.listView1.Location = new System.Drawing.Point(0, 0);
			this.listView1.Name = "listView1";
			this.listView1.Size = new System.Drawing.Size(752, 256);
			this.listView1.TabIndex = 0;
			this.listView1.View = System.Windows.Forms.View.Details;
			this.listView1.SelectedIndexChanged += new System.EventHandler(this.listView1_SelectedIndexChanged);
			// 
			// columnNode
			// 
			this.columnNode.Text = "Node";
			this.columnNode.Width = 200;
			// 
			// columnStatus
			// 
			this.columnStatus.Text = "Status";
			this.columnStatus.Width = 200;
			// 
			// columnUptime
			// 
			this.columnUptime.Text = "Up Time";
			this.columnUptime.Width = 100;
			// 
			// splitter1
			// 
			this.splitter1.Dock = System.Windows.Forms.DockStyle.Top;
			this.splitter1.Location = new System.Drawing.Point(0, 256);
			this.splitter1.Name = "splitter1";
			this.splitter1.Size = new System.Drawing.Size(752, 2);
			this.splitter1.TabIndex = 1;
			this.splitter1.TabStop = false;
			// 
			// panel1
			// 
			this.panel1.Controls.Add(this.pictureBox1);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel1.Location = new System.Drawing.Point(0, 258);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(752, 294);
			this.panel1.TabIndex = 2;
			// 
			// pictureBox1
			// 
			this.pictureBox1.BackColor = System.Drawing.SystemColors.Window;
			this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
			this.pictureBox1.Location = new System.Drawing.Point(0, 0);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(752, 294);
			this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			// 
			// NetworkView
			// 
			this.Controls.Add(this.panel1);
			this.Controls.Add(this.splitter1);
			this.Controls.Add(this.listView1);
			this.Name = "NetworkView";
			this.Size = new System.Drawing.Size(752, 552);
			this.panel1.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion

		private void listView1_SelectedIndexChanged(object sender, System.EventArgs e)
		{
		
		}

		public void PeerManager_NetworkChanged( PeerManager mgr, Servent srv, bool async )
		{	
			string status;
			ListViewItem item;
			string srvGuid = srv.ServentGuid;

			// Add servent node to the list if not already there
			if( m_NodeList.Contains(srvGuid) ) item = (ListViewItem)m_NodeList[srvGuid];
			else
			{
				item = new ListViewItem( new string[]{ srv.RemoteHost, "", "" } );
				item.Tag = srvGuid;
				listView1.Items.Add( item );
				m_NodeList.Add( srvGuid,item );
			}

			// Update item status
			switch ( srv.Status ) 
			{
				case ServentStatus.Connecting:
					status = "Connecting... ";
					item.SubItems[1].Text = status;
					break;

				case ServentStatus.Connected:
					status = "Connected ";
					item.SubItems[1].Text = status;
					break;

				case ServentStatus.Disconnected:
					status = "Disconnected ";
					item.Remove();
					m_NodeList.Remove( srvGuid );
					break;

				default:
					status = "Unknown ";
					break;
			}

			Log.Write( TraceLevel.Info, status + ": " + srv.RemoteHost + ":" + srv.RemotePort,
					   "NetworkView.PeerManager_NetworkChanged" );
		}	
	}
}
