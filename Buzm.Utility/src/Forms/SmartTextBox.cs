using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace Buzm.Utility.Forms
{
	/// <summary>Adds help and focus
	/// text support to TextBox</summary>
	public class SmartTextBox : TextBox
	{
		private string m_UserText;
		private string m_HelpText;

		private bool m_FirstFocus;
		private string m_FocusText;
		private Color m_FocusColor;

		public SmartTextBox() : base()
		{
			m_FirstFocus = true;
			m_HelpText = String.Empty;

			m_FocusText = String.Empty;
			m_FocusColor = ForeColor;
		}

		protected override void OnEnter( EventArgs e )
		{
			if( m_FirstFocus )
			{
				Text = m_FocusText;
				ForeColor = m_FocusColor;
				m_FirstFocus = false;
			}
			base.OnEnter( e ); // raise event
		}

		public new bool Modified
		{
			get // true if user edited
			{
				if( m_UserText == null )
					 return Populated; 
				else return // edited
				  ( m_UserText != Text );
			}
		}

		public bool Populated
		{
			get // true if user text exists
			{
				string trimText = Text.Trim();
				if( trimText != String.Empty )
				{
					if( (trimText == m_HelpText)
					 || (trimText == m_FocusText) ) return false;
					else return true; // user entered text exists
				}
				else return false;
			}
		}

		[ReadOnly( true )]
		public string UserText
		{
			get // user specified text
			{
				if( Modified ) // user edited
				{
					string trimText = Text.Trim();
					if( trimText != String.Empty ) 
						 return trimText;
					else return null;
				}
				else return m_UserText;
			}
			set // user text and init text box
			{
				m_UserText = value; // null matters
				
				if( m_UserText != null ) Text = m_UserText;
				else Text = String.Empty;

				ForeColor = m_FocusColor;
				m_FirstFocus = false;
			}
		}

		[ReadOnly( true )]
		public string PresetText
		{
			set // text if value exists
			{
				if( !String.IsNullOrEmpty( value ) )
				{
					UserText = null;
					Text = value;
				}				
			}
		}

		public Color FocusColor	
		{
			get { return m_FocusColor; }
			set { m_FocusColor = value; }
		}

		public string FocusText
		{
			get { return m_FocusText; }
			set { m_FocusText = value; }
		}

		public string HelpText
		{
			get { return m_HelpText; }
			set { m_HelpText = value; }
		}
	}
}
