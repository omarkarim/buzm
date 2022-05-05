using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using Buzm.Network.Sockets;
using Buzm.Network.Packets;

namespace Buzm.Stresser
{
	/// <summary>
	/// Summary description for Form1.
	/// </summary>
	public class Form1 : System.Windows.Forms.Form
	{
		private string m_HiveID;
		private int m_SendCount;
		private int m_ReceiveCount;
		private int m_MessageSize;
		private long m_AverageLatency;
		private string m_SplitPattern;		
		private string m_DefaultMessage;
		private PeerManager m_PeerManager;

		private System.Windows.Forms.GroupBox m_ConnectGroupBox;
		private System.Windows.Forms.TextBox m_HostTextBox;
		private System.Windows.Forms.TextBox m_PortTextBox;
		private System.Windows.Forms.Label m_HostLabel;
		private System.Windows.Forms.Label m_PortLabel;
		private System.Windows.Forms.Button m_ConnectButton;
		private System.Windows.Forms.Button m_DisconnectButton;
		private System.Windows.Forms.TextBox ServerPortTextBox;
		private System.Windows.Forms.Label ServerPortLabel;
		private System.Windows.Forms.GroupBox m_LoadGroupBox;
		private System.Windows.Forms.Label m_HiveID_label;
		private System.Windows.Forms.TextBox m_HiveID_TextBox;
		private System.Windows.Forms.TextBox m_FrequencyTextBox;
		private System.Windows.Forms.Label m_FrequencyLabel;
		private System.Windows.Forms.Button m_StartButton;
		private System.Windows.Forms.Button m_StopButton;
		private System.Windows.Forms.Timer m_LoadTimer;
		private System.Windows.Forms.GroupBox m_SendMonGroupBox;
		private System.Windows.Forms.Label m_SendPacketsLabel;
		private System.Windows.Forms.Label m_SendPacketsOutputLabel;
		private System.Windows.Forms.Label m_StartTimeLabel;
		private System.Windows.Forms.Label m_StartTimeOutputLabel;
		private System.Windows.Forms.Timer m_ReceiveTimer;
		private System.Windows.Forms.GroupBox m_ReceiveMonGroupBox;
		private System.Windows.Forms.Label m_ReceivePacketsLabel;
		private System.Windows.Forms.Label m_ReceivePacketsOutputLabel;
		private System.Windows.Forms.Label m_LatencyLabel;
		private System.Windows.Forms.Label m_LatencyOutputLabel;
		private System.Windows.Forms.TextBox m_SizeTextBox;
		private System.Windows.Forms.Label m_SizeLabel;
		private System.ComponentModel.IContainer components;

		public Form1()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			
			m_SendCount = 0;
			m_ReceiveCount = 0;
			m_MessageSize = 1;
			m_AverageLatency = 0;
			m_SplitPattern = "|";			

			// This a 500 character / 1KB message.
			m_DefaultMessage = "Martha Stewart Living Omnimedia Inc. said Stewart, who faces a possible prison sentence after being found guilty on four criminal counts, would take on a new post, founding editorial director. Stewart, 62, is the public face behind the media and merchandising empire, whose reach extends to magazines, TV programs and household products sold at Kmart stores. Industry analysts had said previously that Stewart's resignation from the board was almost a certainty because shes is now a convicted felon.";
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

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.m_ConnectGroupBox = new System.Windows.Forms.GroupBox();
			this.ServerPortLabel = new System.Windows.Forms.Label();
			this.ServerPortTextBox = new System.Windows.Forms.TextBox();
			this.m_DisconnectButton = new System.Windows.Forms.Button();
			this.m_ConnectButton = new System.Windows.Forms.Button();
			this.m_PortLabel = new System.Windows.Forms.Label();
			this.m_HostLabel = new System.Windows.Forms.Label();
			this.m_PortTextBox = new System.Windows.Forms.TextBox();
			this.m_HostTextBox = new System.Windows.Forms.TextBox();
			this.m_LoadGroupBox = new System.Windows.Forms.GroupBox();
			this.m_StopButton = new System.Windows.Forms.Button();
			this.m_StartButton = new System.Windows.Forms.Button();
			this.m_SizeTextBox = new System.Windows.Forms.TextBox();
			this.m_SizeLabel = new System.Windows.Forms.Label();
			this.m_FrequencyLabel = new System.Windows.Forms.Label();
			this.m_FrequencyTextBox = new System.Windows.Forms.TextBox();
			this.m_HiveID_TextBox = new System.Windows.Forms.TextBox();
			this.m_HiveID_label = new System.Windows.Forms.Label();
			this.m_LoadTimer = new System.Windows.Forms.Timer(this.components);
			this.m_SendMonGroupBox = new System.Windows.Forms.GroupBox();
			this.m_StartTimeOutputLabel = new System.Windows.Forms.Label();
			this.m_StartTimeLabel = new System.Windows.Forms.Label();
			this.m_SendPacketsOutputLabel = new System.Windows.Forms.Label();
			this.m_SendPacketsLabel = new System.Windows.Forms.Label();
			this.m_ReceiveTimer = new System.Windows.Forms.Timer(this.components);
			this.m_ReceiveMonGroupBox = new System.Windows.Forms.GroupBox();
			this.m_LatencyOutputLabel = new System.Windows.Forms.Label();
			this.m_LatencyLabel = new System.Windows.Forms.Label();
			this.m_ReceivePacketsOutputLabel = new System.Windows.Forms.Label();
			this.m_ReceivePacketsLabel = new System.Windows.Forms.Label();
			this.m_ConnectGroupBox.SuspendLayout();
			this.m_LoadGroupBox.SuspendLayout();
			this.m_SendMonGroupBox.SuspendLayout();
			this.m_ReceiveMonGroupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// m_ConnectGroupBox
			// 
			this.m_ConnectGroupBox.Controls.Add(this.ServerPortLabel);
			this.m_ConnectGroupBox.Controls.Add(this.ServerPortTextBox);
			this.m_ConnectGroupBox.Controls.Add(this.m_DisconnectButton);
			this.m_ConnectGroupBox.Controls.Add(this.m_ConnectButton);
			this.m_ConnectGroupBox.Controls.Add(this.m_PortLabel);
			this.m_ConnectGroupBox.Controls.Add(this.m_HostLabel);
			this.m_ConnectGroupBox.Controls.Add(this.m_PortTextBox);
			this.m_ConnectGroupBox.Controls.Add(this.m_HostTextBox);
			this.m_ConnectGroupBox.Location = new System.Drawing.Point(8, 8);
			this.m_ConnectGroupBox.Name = "m_ConnectGroupBox";
			this.m_ConnectGroupBox.Size = new System.Drawing.Size(448, 104);
			this.m_ConnectGroupBox.TabIndex = 0;
			this.m_ConnectGroupBox.TabStop = false;
			this.m_ConnectGroupBox.Text = "Connection";
			// 
			// ServerPortLabel
			// 
			this.ServerPortLabel.Location = new System.Drawing.Point(8, 71);
			this.ServerPortLabel.Name = "ServerPortLabel";
			this.ServerPortLabel.Size = new System.Drawing.Size(72, 23);
			this.ServerPortLabel.TabIndex = 7;
			this.ServerPortLabel.Text = "Local Port:";
			// 
			// ServerPortTextBox
			// 
			this.ServerPortTextBox.Location = new System.Drawing.Point(88, 72);
			this.ServerPortTextBox.Name = "ServerPortTextBox";
			this.ServerPortTextBox.Size = new System.Drawing.Size(200, 20);
			this.ServerPortTextBox.TabIndex = 3;
			this.ServerPortTextBox.Text = "Enter a port to listen on...";
			// 
			// m_DisconnectButton
			// 
			this.m_DisconnectButton.Location = new System.Drawing.Point(304, 64);
			this.m_DisconnectButton.Name = "m_DisconnectButton";
			this.m_DisconnectButton.Size = new System.Drawing.Size(128, 32);
			this.m_DisconnectButton.TabIndex = 5;
			this.m_DisconnectButton.Text = "Disconnect";
			this.m_DisconnectButton.Click += new System.EventHandler(this.m_DisconnectButton_Click);
			// 
			// m_ConnectButton
			// 
			this.m_ConnectButton.Location = new System.Drawing.Point(304, 24);
			this.m_ConnectButton.Name = "m_ConnectButton";
			this.m_ConnectButton.Size = new System.Drawing.Size(128, 32);
			this.m_ConnectButton.TabIndex = 4;
			this.m_ConnectButton.Text = "Connect";
			this.m_ConnectButton.Click += new System.EventHandler(this.m_ConnectButton_Click);
			// 
			// m_PortLabel
			// 
			this.m_PortLabel.Location = new System.Drawing.Point(8, 47);
			this.m_PortLabel.Name = "m_PortLabel";
			this.m_PortLabel.Size = new System.Drawing.Size(72, 23);
			this.m_PortLabel.TabIndex = 3;
			this.m_PortLabel.Text = "Remote Port:";
			// 
			// m_HostLabel
			// 
			this.m_HostLabel.Location = new System.Drawing.Point(8, 23);
			this.m_HostLabel.Name = "m_HostLabel";
			this.m_HostLabel.Size = new System.Drawing.Size(80, 23);
			this.m_HostLabel.TabIndex = 2;
			this.m_HostLabel.Text = "Remote Host:";
			// 
			// m_PortTextBox
			// 
			this.m_PortTextBox.Location = new System.Drawing.Point(88, 48);
			this.m_PortTextBox.Name = "m_PortTextBox";
			this.m_PortTextBox.Size = new System.Drawing.Size(200, 20);
			this.m_PortTextBox.TabIndex = 1;
			this.m_PortTextBox.Text = "Enter a port to connect to...";
			// 
			// m_HostTextBox
			// 
			this.m_HostTextBox.Location = new System.Drawing.Point(88, 24);
			this.m_HostTextBox.Name = "m_HostTextBox";
			this.m_HostTextBox.Size = new System.Drawing.Size(200, 20);
			this.m_HostTextBox.TabIndex = 0;
			this.m_HostTextBox.Text = "Enter a hostname to connect to...";
			// 
			// m_LoadGroupBox
			// 
			this.m_LoadGroupBox.Controls.Add(this.m_StopButton);
			this.m_LoadGroupBox.Controls.Add(this.m_StartButton);
			this.m_LoadGroupBox.Controls.Add(this.m_SizeTextBox);
			this.m_LoadGroupBox.Controls.Add(this.m_SizeLabel);
			this.m_LoadGroupBox.Controls.Add(this.m_FrequencyLabel);
			this.m_LoadGroupBox.Controls.Add(this.m_FrequencyTextBox);
			this.m_LoadGroupBox.Controls.Add(this.m_HiveID_TextBox);
			this.m_LoadGroupBox.Controls.Add(this.m_HiveID_label);
			this.m_LoadGroupBox.Location = new System.Drawing.Point(8, 120);
			this.m_LoadGroupBox.Name = "m_LoadGroupBox";
			this.m_LoadGroupBox.Size = new System.Drawing.Size(448, 104);
			this.m_LoadGroupBox.TabIndex = 1;
			this.m_LoadGroupBox.TabStop = false;
			this.m_LoadGroupBox.Text = "Load";
			// 
			// m_StopButton
			// 
			this.m_StopButton.Location = new System.Drawing.Point(304, 64);
			this.m_StopButton.Name = "m_StopButton";
			this.m_StopButton.Size = new System.Drawing.Size(128, 32);
			this.m_StopButton.TabIndex = 7;
			this.m_StopButton.Text = "Stop";
			this.m_StopButton.Click += new System.EventHandler(this.m_StopButton_Click);
			// 
			// m_StartButton
			// 
			this.m_StartButton.Location = new System.Drawing.Point(304, 24);
			this.m_StartButton.Name = "m_StartButton";
			this.m_StartButton.Size = new System.Drawing.Size(128, 32);
			this.m_StartButton.TabIndex = 6;
			this.m_StartButton.Text = "Start";
			this.m_StartButton.Click += new System.EventHandler(this.m_StartButton_Click);
			// 
			// m_SizeTextBox
			// 
			this.m_SizeTextBox.Location = new System.Drawing.Point(88, 72);
			this.m_SizeTextBox.Name = "m_SizeTextBox";
			this.m_SizeTextBox.Size = new System.Drawing.Size(200, 20);
			this.m_SizeTextBox.TabIndex = 5;
			this.m_SizeTextBox.Text = "Enter message size in kilobytes..";
			// 
			// m_SizeLabel
			// 
			this.m_SizeLabel.Location = new System.Drawing.Point(8, 72);
			this.m_SizeLabel.Name = "m_SizeLabel";
			this.m_SizeLabel.Size = new System.Drawing.Size(80, 23);
			this.m_SizeLabel.TabIndex = 4;
			this.m_SizeLabel.Text = "Packet Size:";
			// 
			// m_FrequencyLabel
			// 
			this.m_FrequencyLabel.Location = new System.Drawing.Point(8, 48);
			this.m_FrequencyLabel.Name = "m_FrequencyLabel";
			this.m_FrequencyLabel.Size = new System.Drawing.Size(64, 23);
			this.m_FrequencyLabel.TabIndex = 3;
			this.m_FrequencyLabel.Text = "Frequency:";
			// 
			// m_FrequencyTextBox
			// 
			this.m_FrequencyTextBox.Location = new System.Drawing.Point(88, 48);
			this.m_FrequencyTextBox.Name = "m_FrequencyTextBox";
			this.m_FrequencyTextBox.Size = new System.Drawing.Size(200, 20);
			this.m_FrequencyTextBox.TabIndex = 2;
			this.m_FrequencyTextBox.Text = "Enter msecs to wait between packets...";
			// 
			// m_HiveID_TextBox
			// 
			this.m_HiveID_TextBox.Location = new System.Drawing.Point(88, 24);
			this.m_HiveID_TextBox.Name = "m_HiveID_TextBox";
			this.m_HiveID_TextBox.Size = new System.Drawing.Size(200, 20);
			this.m_HiveID_TextBox.TabIndex = 1;
			this.m_HiveID_TextBox.Text = "Enter a Hive identity to simulate...";
			// 
			// m_HiveID_label
			// 
			this.m_HiveID_label.Location = new System.Drawing.Point(8, 24);
			this.m_HiveID_label.Name = "m_HiveID_label";
			this.m_HiveID_label.Size = new System.Drawing.Size(48, 23);
			this.m_HiveID_label.TabIndex = 0;
			this.m_HiveID_label.Text = "Hive ID:";
			// 
			// m_LoadTimer
			// 
			this.m_LoadTimer.Tick += new System.EventHandler(this.m_LoadTimer_Tick);
			// 
			// m_SendMonGroupBox
			// 
			this.m_SendMonGroupBox.Controls.Add(this.m_StartTimeOutputLabel);
			this.m_SendMonGroupBox.Controls.Add(this.m_StartTimeLabel);
			this.m_SendMonGroupBox.Controls.Add(this.m_SendPacketsOutputLabel);
			this.m_SendMonGroupBox.Controls.Add(this.m_SendPacketsLabel);
			this.m_SendMonGroupBox.Location = new System.Drawing.Point(8, 232);
			this.m_SendMonGroupBox.Name = "m_SendMonGroupBox";
			this.m_SendMonGroupBox.Size = new System.Drawing.Size(216, 80);
			this.m_SendMonGroupBox.TabIndex = 2;
			this.m_SendMonGroupBox.TabStop = false;
			this.m_SendMonGroupBox.Text = "Send Statistics";
			// 
			// m_StartTimeOutputLabel
			// 
			this.m_StartTimeOutputLabel.Location = new System.Drawing.Point(88, 48);
			this.m_StartTimeOutputLabel.Name = "m_StartTimeOutputLabel";
			this.m_StartTimeOutputLabel.Size = new System.Drawing.Size(80, 23);
			this.m_StartTimeOutputLabel.TabIndex = 3;
			this.m_StartTimeOutputLabel.Text = "12:00 AM";
			// 
			// m_StartTimeLabel
			// 
			this.m_StartTimeLabel.Location = new System.Drawing.Point(8, 48);
			this.m_StartTimeLabel.Name = "m_StartTimeLabel";
			this.m_StartTimeLabel.TabIndex = 2;
			this.m_StartTimeLabel.Text = "Start Time:";
			// 
			// m_SendPacketsOutputLabel
			// 
			this.m_SendPacketsOutputLabel.Location = new System.Drawing.Point(88, 24);
			this.m_SendPacketsOutputLabel.Name = "m_SendPacketsOutputLabel";
			this.m_SendPacketsOutputLabel.Size = new System.Drawing.Size(80, 23);
			this.m_SendPacketsOutputLabel.TabIndex = 1;
			this.m_SendPacketsOutputLabel.Text = "0";
			// 
			// m_SendPacketsLabel
			// 
			this.m_SendPacketsLabel.Location = new System.Drawing.Point(8, 24);
			this.m_SendPacketsLabel.Name = "m_SendPacketsLabel";
			this.m_SendPacketsLabel.Size = new System.Drawing.Size(48, 23);
			this.m_SendPacketsLabel.TabIndex = 0;
			this.m_SendPacketsLabel.Text = "Packets:";
			// 
			// m_ReceiveTimer
			// 
			this.m_ReceiveTimer.Enabled = true;
			this.m_ReceiveTimer.Interval = 1000;
			this.m_ReceiveTimer.Tick += new System.EventHandler(this.m_ReceiveTimer_Tick);
			// 
			// m_ReceiveMonGroupBox
			// 
			this.m_ReceiveMonGroupBox.Controls.Add(this.m_LatencyOutputLabel);
			this.m_ReceiveMonGroupBox.Controls.Add(this.m_LatencyLabel);
			this.m_ReceiveMonGroupBox.Controls.Add(this.m_ReceivePacketsOutputLabel);
			this.m_ReceiveMonGroupBox.Controls.Add(this.m_ReceivePacketsLabel);
			this.m_ReceiveMonGroupBox.Location = new System.Drawing.Point(240, 232);
			this.m_ReceiveMonGroupBox.Name = "m_ReceiveMonGroupBox";
			this.m_ReceiveMonGroupBox.Size = new System.Drawing.Size(216, 80);
			this.m_ReceiveMonGroupBox.TabIndex = 3;
			this.m_ReceiveMonGroupBox.TabStop = false;
			this.m_ReceiveMonGroupBox.Text = "Receive Statistics";
			// 
			// m_LatencyOutputLabel
			// 
			this.m_LatencyOutputLabel.Location = new System.Drawing.Point(112, 48);
			this.m_LatencyOutputLabel.Name = "m_LatencyOutputLabel";
			this.m_LatencyOutputLabel.TabIndex = 3;
			this.m_LatencyOutputLabel.Text = "0";
			// 
			// m_LatencyLabel
			// 
			this.m_LatencyLabel.Location = new System.Drawing.Point(16, 48);
			this.m_LatencyLabel.Name = "m_LatencyLabel";
			this.m_LatencyLabel.Size = new System.Drawing.Size(96, 23);
			this.m_LatencyLabel.TabIndex = 2;
			this.m_LatencyLabel.Text = "Latency (msecs):";
			// 
			// m_ReceivePacketsOutputLabel
			// 
			this.m_ReceivePacketsOutputLabel.Location = new System.Drawing.Point(112, 24);
			this.m_ReceivePacketsOutputLabel.Name = "m_ReceivePacketsOutputLabel";
			this.m_ReceivePacketsOutputLabel.TabIndex = 1;
			this.m_ReceivePacketsOutputLabel.Text = "0";
			// 
			// m_ReceivePacketsLabel
			// 
			this.m_ReceivePacketsLabel.Location = new System.Drawing.Point(16, 24);
			this.m_ReceivePacketsLabel.Name = "m_ReceivePacketsLabel";
			this.m_ReceivePacketsLabel.Size = new System.Drawing.Size(56, 23);
			this.m_ReceivePacketsLabel.TabIndex = 0;
			this.m_ReceivePacketsLabel.Text = "Packets:";
			// 
			// Form1
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(464, 319);
			this.Controls.Add(this.m_ReceiveMonGroupBox);
			this.Controls.Add(this.m_SendMonGroupBox);
			this.Controls.Add(this.m_LoadGroupBox);
			this.Controls.Add(this.m_ConnectGroupBox);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Name = "Form1";
			this.Text = " Buzm Stresser Tool";
			this.m_ConnectGroupBox.ResumeLayout(false);
			this.m_LoadGroupBox.ResumeLayout(false);
			this.m_SendMonGroupBox.ResumeLayout(false);
			this.m_ReceiveMonGroupBox.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			Application.Run(new Form1());
		}

		private void m_ConnectButton_Click(object sender, System.EventArgs e)
		{
			if( m_PeerManager != null ) m_PeerManager.Close();
			
			try 
			{
				string remoteHost = m_HostTextBox.Text;
				int remotePort = Convert.ToInt32( m_PortTextBox.Text );
				int serverPort = Convert.ToInt32( ServerPortTextBox.Text );

				m_PeerManager  = new PeerManager( serverPort, this );
				m_PeerManager.CreateServentAsync( remoteHost, remotePort );
			}
			catch( Exception )
			{
				MessageBox.Show( "Please enter valid connection settings.", "Buzm.Stresser", 
					MessageBoxButtons.OK, MessageBoxIcon.Exclamation );
			}
		}

		private void m_DisconnectButton_Click(object sender, System.EventArgs e)
		{
			m_LoadTimer.Stop();
			if( m_PeerManager != null ) m_PeerManager.Close();
		}

		private void m_StartButton_Click(object sender, System.EventArgs e)
		{
			m_SendCount = 0;
			m_ReceiveCount = 0;
			m_AverageLatency = 0;

			m_HiveID = m_HiveID_TextBox.Text;
			m_PeerManager.RegisterHive( m_HiveID );
			
			try 
			{ 
				m_MessageSize = Convert.ToInt32( m_SizeTextBox.Text );
				int frequency = Convert.ToInt32( m_FrequencyTextBox.Text );
				
				m_LoadTimer.Interval = frequency;			
				m_LoadTimer.Start();
				m_StartTimeOutputLabel.Text = DateTime.Now.ToShortTimeString();
			}
			catch( Exception )
			{
				MessageBox.Show( "Please enter valid load settings.", "Buzm.Stresser", 
					MessageBoxButtons.OK, MessageBoxIcon.Exclamation );
			}
		}

		private void m_StopButton_Click(object sender, System.EventArgs e)
		{
			m_LoadTimer.Stop();
		}

		private void m_LoadTimer_Tick(object sender, System.EventArgs e)
		{
			Packet pkt = new Packet( EncodeMessage( m_MessageSize ), m_HiveID );
			m_PeerManager.SendToServents( pkt );
			m_SendCount++;
			m_SendPacketsOutputLabel.Text = m_SendCount.ToString();
		}

		private void m_ReceiveTimer_Tick(object sender, System.EventArgs e)
		{
			Packet pkt;
			if( m_PeerManager != null )
			{
				while( (pkt = m_PeerManager.GetNextPacket()) != null )
				{ 
					m_ReceiveCount++;
					m_ReceivePacketsOutputLabel.Text = m_ReceiveCount.ToString();
					int latencyMsecs = CalculateLatency( pkt.ToString() );
					m_LatencyOutputLabel.Text = latencyMsecs.ToString();
				}
			}
		}

		private string EncodeMessage( int messageSize )
		{
			string message = DateTime.Now.Ticks.ToString() + m_SplitPattern;
			
			// Assuming default message is 1KB
			for( int i=0; i < messageSize; i++ )
			{
				message += m_DefaultMessage;
			}
			return message;
		}

		private int CalculateLatency( string message )
		{
			try
			{
				string[] headers = message.Split( m_SplitPattern.ToCharArray(), 2 );
				long sendTicks = Convert.ToInt64( headers[0] );
				long latencyTicks = DateTime.Now.Ticks - sendTicks;
				m_AverageLatency = (m_AverageLatency + latencyTicks) / m_ReceiveCount;				
			}
			catch( Exception )
			{
				MessageBox.Show( "Invalid time in message header.", "Buzm.Stresser", 
					MessageBoxButtons.OK, MessageBoxIcon.Exclamation );
			}
			return (int)(m_AverageLatency / 10000);
		}
	}
}
