using System;
using System.Drawing;
using System.Diagnostics;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Buzm.Network.Web;
using Buzm.Utility;
using Buzm.Hives;

namespace Buzm
{
	//[System.Runtime.InteropServices.ComVisibleAttribute( true )]
	public class HiveView : System.Windows.Forms.UserControl 
	{
		private string m_CurrentUrl;
		private string m_DefaultUrl;
		
		private Panel m_BrowserPanel;
		private Random m_RandFactory;
		
		//private WebBrowser m_WebBrowser;
		private HiveModel m_CurrentHive;
		public event RestEventHandler BrowserClick;

		public HiveView() 
		{
			m_RandFactory = new Random(); // used for cache-busting urls
			m_DefaultUrl = Config.GetFileValue( "preferences/defaultView" );
			m_CurrentUrl = m_DefaultUrl; // start with the default page
	
			InitializeComponent(); // studio forms designer code			
			EnableScripting(); // enable browser callback messages
			LoadPage( m_CurrentUrl ); // load default page in browser
		}

		public void GoHome()
		{
			// load hive home page
			LoadPage( m_CurrentUrl );
		}

		public void ClearView()
		{
			m_CurrentHive = null; // clear hive
			if( m_CurrentUrl != m_DefaultUrl )
			{
				m_CurrentUrl = m_DefaultUrl;
				LoadPage( m_CurrentUrl );
			}
		}

		private void LoadPage( string url )
		{
			try // loading the url in web browser
			{
				// string rndUrl = url + "?rnd=" + m_RandFactory.Next().ToString();
				//m_WebBrowser.Navigate( url ); // ie7 breaks on query string
			}
			catch( Exception e )
			{
				Log.Write( "Failed to load requested url: " + url,
				TraceLevel.Error, "HiveView.LoadPage", e );
			}
		}

		private void Reload() 
		{
			try // reloading the current page
			{
				// refresh the page from source rather than cache
				//m_WebBrowser.Refresh( WebBrowserRefreshOption.Completely );				
			}
			catch( Exception e )
			{
				Log.Write( "Failed to reload url: " + m_CurrentUrl,
				TraceLevel.Warning, "HiveView.Reload", e );
			}			
		}

		public void HiveManager_HiveSelected( object sender, ModelEventArgs e )
		{
			m_CurrentHive = (HiveModel)e.Model;
			m_CurrentUrl = m_CurrentHive.Url;
			LoadPage( m_CurrentUrl );
		}

		public void HiveManager_HiveUpdated( object sender, ModelEventArgs e )
		{
			// reload if current hive was updated
			if( m_CurrentHive == e.Model ) Reload();
		}

		/// <summary>Maps string params to a REST event and notifies 
		/// listeners. Designed to be called from within the browser
		/// using the window.external DOM object provided by IE</summary>		
		public void OnBrowserClick( string method, string uri, string data )
		{
			try // to raise browser click event for listeners
			{
				RestEventArgs args = new RestEventArgs( method, uri, data );
				if( BrowserClick != null ) BrowserClick( this, args );
			}
			catch( Exception e )
			{
				Log.Write( "Failed to raise browser click event",
				TraceLevel.Warning, "HiveView.OnBrowserClick", e );
			}
		}

		public static void OpenWithNewBrowser( string url )
		{
			try // to open url using default browser
			{
				System.Diagnostics.Process.Start( url );
			}
			catch( Exception e )
			{
				Log.Write( "Could not open url in browser - " + url,
				TraceLevel.Warning, "HiveView.OpenWithNewBrowser", e );
			}
		}

		private void EnableScripting()
		{
			try // setting window.external callback
			{
				//m_WebBrowser.ObjectForScripting = this;
				//m_WebBrowser.ScriptErrorsSuppressed = true;
			}
			catch( Exception e )
			{
				Log.Write( "Failed to enable script callbacks",
				TraceLevel.Error, "HiveView.EnableScripting", e );
			}
		}

		private void InitializeComponent()
		{
//			this.m_BrowserPanel = new System.Windows.Forms.Panel();
//			this.m_WebBrowser = new System.Windows.Forms.WebBrowser();
//			this.m_BrowserPanel.SuspendLayout();
//			this.SuspendLayout();
//			// 
//			// m_BrowserPanel
//			// 
//			this.m_BrowserPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
//			this.m_BrowserPanel.Controls.Add(this.m_WebBrowser);
//			this.m_BrowserPanel.Dock = System.Windows.Forms.DockStyle.Fill;
//			this.m_BrowserPanel.Location = new System.Drawing.Point(0, 0);
//			this.m_BrowserPanel.Name = "m_BrowserPanel";
//			this.m_BrowserPanel.Size = new System.Drawing.Size(656, 512);
//			this.m_BrowserPanel.TabIndex = 0;
//			// 
//			// m_WebBrowser
//			// 
//			this.m_WebBrowser.AllowWebBrowserDrop = false;
//			this.m_WebBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
//			this.m_WebBrowser.Location = new System.Drawing.Point(0, 0);
//			this.m_WebBrowser.MinimumSize = new System.Drawing.Size(20, 20);
//			this.m_WebBrowser.Name = "m_WebBrowser";
//			this.m_WebBrowser.Size = new System.Drawing.Size(652, 508);
//			this.m_WebBrowser.TabIndex = 0;
//			// 
//			// HiveView
//			// 
//			this.Controls.Add(this.m_BrowserPanel);
//			this.Name = "HiveView";
//			this.Size = new System.Drawing.Size(656, 512);
//			this.m_BrowserPanel.ResumeLayout(false);
//			this.ResumeLayout(false);

		}
	}
}
