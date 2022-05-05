using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Buzm.Utility;

namespace Buzm.Schemas
{
	/// <summary>Base class for serializable hive
	///  content types such as item and channel</summary>
	public abstract class ContentType : BaseType
	{
		protected string m_Guid;
		protected string m_Link;
		protected string m_Tags;
		protected string m_Title;				
		protected string m_Summary;		
		protected string m_Position;
		protected string m_Priority;
		protected DateTime m_Posted;
		protected DateTime m_Expires;
		protected DateTime m_Modified;
				
		private const int EXPIRE_MAX_YEARS = 100;
		private const string DEFAULT_PRIORITY = "50";		

		public ContentType()
		{
			m_Modified = DateTime.Now;
			m_Expires = m_Modified;
			m_Posted = m_Modified;			
			m_Priority = DEFAULT_PRIORITY;
			m_Guid = System.Guid.NewGuid().ToString();
		}

		/// <summary>Sets expire date to the 
		/// maximum permissible value </summary>
		public void SetMaxExpireDate()
		{
			m_Expires = m_Expires.AddYears( EXPIRE_MAX_YEARS );
		}

		#region Serialized Properties

		[XmlElement("guid", Form=XmlSchemaForm.Unqualified)] public string Guid 
		{ get { return RemoveIllegalChars( m_Guid ); } set { m_Guid = value; } }

		[XmlAnyElement("title")] public XmlElement TitleElement
		{
			get { return CreateCDataElement( "title", m_Title ); }
			set { m_Title = value.InnerText; /* element text */ }
		}

		[XmlAnyElement("link")] public XmlElement LinkElement
		{
			get { return CreateCDataElement( "link", m_Link ); }
			set { m_Link = value.InnerText; /* element text */ }
		}

		[XmlAnyElement( "tags" )] public XmlElement TagsElement
		{
			get { return CreateCDataElement( "tags", m_Tags ); }
			set { m_Tags = value.InnerText; /* element text */ }
		}

		[XmlAnyElement("summary")] public XmlElement SummaryElement
		{
			get { return CreateCDataElement( "summary", m_Summary ); }
			set { m_Summary = value.InnerText; /* element text */ }
		}
		
		[XmlElement("modified")] public string ModifiedString
		{ 
			get { return Format.DateToString( m_Modified ); }
			set { m_Modified = Format.StringToDate( value ); }
		}

		[XmlElement("expires")] public string ExpiresString
		{
			get { return Format.DateToString( m_Expires ); }
			set { m_Expires = Format.StringToDate( value ); }
		}

		[XmlElement("posted")] public string PostedString
		{
			get { return Format.DateToString( m_Posted ); }
			set { m_Posted = Format.StringToDate( value ); }
		}
		    
		[XmlElement("position", Form=XmlSchemaForm.Unqualified)] public string Position 
		{ get { return RemoveIllegalChars( m_Position ); } set { m_Position = value; } }

		[XmlElement("priority", Form=XmlSchemaForm.Unqualified)] public string Priority 
		{ get { return RemoveIllegalChars( m_Priority ); } set { m_Priority = value; } }

		# endregion

		#region Unserialized Properties

		[XmlIgnore] public string Link { get { return m_Link; } set { m_Link = value; } }
		[XmlIgnore] public string Tags { get { return m_Tags; } set { m_Tags = value; } }
		[XmlIgnore] public string Title { get { return m_Title; } set { m_Title = value; } }
		[XmlIgnore] public string Summary { get { return m_Summary; } set { m_Summary = value; } }		
		[XmlIgnore] public DateTime Modified { get { return m_Modified; } set { m_Modified = value; } }
		[XmlIgnore] public DateTime Expires { get { return m_Expires; } set { m_Expires = value; } }
		[XmlIgnore] public DateTime Posted { get { return m_Posted; } set { m_Posted = value; } }
		
		# endregion
	}
}
