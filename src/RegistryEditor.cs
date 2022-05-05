using System;
using System.Threading;
using System.ComponentModel;
using System.Windows.Forms;
using Buzm.Register;

namespace Buzm
{
	/// <summary>Base class for registry editing forms. Should be abstract
	/// but the VS Forms Designer complains if the attribute is set</summary>
	public class RegistryEditor : System.Windows.Forms.Form
	{
		private bool m_ShowErrors;
		private User m_ActionUser;
		private string m_ActionGuid;
		private RegistryAction m_Action;
		private RegistryResult m_LastResult;
				
		// max time to wait for action to complete
		private const int REGISTRY_TIMEOUT = 60000;
		private const int PROGRESS_ANIMATION_SLEEP = 0;
		public event RegistryEventHandler RegistryRequest;
		private static event RegistryEventHandler RegistryResponse;
		
		protected System.Windows.Forms.Timer m_ActionTimer;
		protected System.Windows.Forms.ProgressBar m_ActionProgressBar;
		private System.ComponentModel.IContainer components;
		
		public RegistryEditor()
		{
			m_ShowErrors = true;
			m_ActionUser = new User();
			m_Action = RegistryAction.None;
			m_LastResult = RegistryResult.None;
			m_ActionGuid = Guid.NewGuid().ToString();
			InitializeComponent(); // forms designer code

			// bind to static registry response event to receive results asynchronously 
			RegistryResponse += new RegistryEventHandler( RegistryEditor_RegistryResponse );
		}
		
		protected void BeginRegistryRequest( )
		{
			m_LastResult = RegistryResult.None;
			m_ActionGuid = Guid.NewGuid().ToString();		
			
			// timer gradually moves the progress bar to max based on default timeout
			m_ActionTimer.Interval = ( REGISTRY_TIMEOUT / m_ActionProgressBar.Maximum );
			m_ActionProgressBar.Value = m_ActionProgressBar.Minimum;
			m_ActionProgressBar.Visible = true;
			m_ActionTimer.Start();
			
			RegistryEventArgs e = new RegistryEventArgs( m_ActionUser, m_Action, m_ActionGuid );
			OnRegistryRequest( e ); // fire event for a local or remote peer Registry to handle
		}

		/// <summary>Completes process started by BeginRegistryRequest</summary>
		/// <param name="e">RegistryEventArgs for the registry result</param>
		/// <returns>True if request completed, otherwise false</returns>
		protected bool EndRegistryRequest( RegistryEventArgs e )
		{
			// action results are passed to all active editors so
			if( e.ActionGuid == m_ActionGuid ) // if local action
			{
				m_LastResult = e.Result; // save for retry timer
				if( m_LastResult != RegistryResult.NetworkError )
				{
					m_ActionTimer.Stop(); // stop retries		
					m_ActionGuid = Guid.NewGuid().ToString();

					// if this request was not locally cancelled
					if( m_LastResult != RegistryResult.Cancelled )
					{
						// fast forward the progress bar to the end before hiding it
						while( m_ActionProgressBar.Value < m_ActionProgressBar.Maximum )
						{
							Thread.Sleep( PROGRESS_ANIMATION_SLEEP );
							m_ActionProgressBar.PerformStep(); 
							m_ActionProgressBar.Update();						
						}
					}
					m_ActionProgressBar.Visible = false;
					m_LastResult = RegistryResult.None;
					return true;
				}
				else return false; // the request is incomplete
			}
			else return false; // result does not match request
		}

		protected void CancelRegistryRequest( )
		{
			RegistryEventArgs e = new RegistryEventArgs( m_ActionUser, RegistryResult.Cancelled, 
			"Request was cancelled - Please try again if needed", m_ActionGuid );
			EndRegistryRequest( e ); // force request to complete
		}

		private void m_ActionTimer_Tick( object sender, System.EventArgs e )
		{
			m_ActionProgressBar.PerformStep(); // increment progress bar
			if ( m_ActionProgressBar.Value == m_ActionProgressBar.Maximum )
			{	
				CancelRegistryRequest(); // The action has timed-out so stop the loop and clear status
				if( ShowErrors ) AlertUser( "Unable to contact the Buzm network - Please check your internet connection and try again." );
				Close(); // hide the registration window from view
			}
			else // determine if current request should be retried
			{	
				if( m_LastResult == RegistryResult.NetworkError )
				{
					m_LastResult = RegistryResult.None; // request failed so we clear and try again
					OnRegistryRequest( new RegistryEventArgs( m_ActionUser, m_Action, m_ActionGuid ) );
				}
			}
		}

		protected void OnRegistryRequest( RegistryEventArgs e )
		{
			// proxy method allows sub-classes to raise the event
			if( RegistryRequest != null ) RegistryRequest( this, e );
		}

		public static void OnRegistryResponse( object sender, RegistryEventArgs e )
		{
			if( RegistryResponse != null ) RegistryResponse( sender, e );
		}

		// handler should be overriden by derived classes to complete registry request cycle
		public virtual void RegistryEditor_RegistryResponse( object sender, RegistryEventArgs e ){}

		protected void ClearRegistryEvents()
		{
			// unsubscribe instance from static handler so memory can be released
			RegistryResponse -= new RegistryEventHandler( RegistryEditor_RegistryResponse );
		}
		
		private void RegistryEditor_Closing( object sender, CancelEventArgs e )
		{
			CancelRegistryRequest(); // stops the progress bar timer
			ClearRegistryEvents(); // unsubscribe from registry events
		}

		protected void AlertUser( string message )
		{
			// display a message box to the user
			MessageBox.Show( this, message, "Buzm Alert", 
			MessageBoxButtons.OK, MessageBoxIcon.Information );
		}

		public RegistryAction Action
		{
			get { return m_Action; }
			set { m_Action = value; }
		}

		public string ActionGuid
		{ 
			get { return m_ActionGuid; } 
			set { m_ActionGuid = value; }
		}

		public User ActionUser
		{
			get { return m_ActionUser; }
			set { m_ActionUser = value; }
		}

		public RegistryResult LastResult
		{
			get { return m_LastResult; }
			set { m_LastResult = value; }
		}

		public bool ShowErrors
		{
			get { return m_ShowErrors; }
			set { m_ShowErrors = value; }
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.m_ActionProgressBar = new System.Windows.Forms.ProgressBar();
			this.m_ActionTimer = new System.Windows.Forms.Timer(this.components);
			this.SuspendLayout();
			// 
			// m_ActionProgressBar
			// 
			this.m_ActionProgressBar.Location = new System.Drawing.Point(56, 88);
			this.m_ActionProgressBar.Name = "m_ActionProgressBar";
			this.m_ActionProgressBar.Size = new System.Drawing.Size(264, 24);
			this.m_ActionProgressBar.Step = 1;
			this.m_ActionProgressBar.TabIndex = 0;
			this.m_ActionProgressBar.Visible = false;
			// 
			// m_ActionTimer
			// 
			this.m_ActionTimer.Interval = 500;
			this.m_ActionTimer.Tick += new System.EventHandler(this.m_ActionTimer_Tick);
			// 
			// RegistryEditor
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(408, 221);
			this.Controls.Add(this.m_ActionProgressBar);
			this.Name = "RegistryEditor";
			this.Text = "Registry Editor Base Class";
			this.Closing += new System.ComponentModel.CancelEventHandler(this.RegistryEditor_Closing);
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
