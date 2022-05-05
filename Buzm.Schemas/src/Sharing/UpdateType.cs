using System;
using System.Xml.Schema;
using System.Xml.Serialization;
using Buzm.Utility;

namespace Buzm.Schemas.Sharing
{
	[XmlRootAttribute( "update", Namespace = "http://www.microsoft.com/schemas/rss/sse" )]
	public class UpdateType
	{
		private string m_By;
		private DateTime m_When;

		public UpdateType(){ }

		public bool IsMatch( UpdateType update )
		{
			if( ( update != null ) &&
				( m_When == update.When ) &&
				String.Equals( m_By, update.By,
				StringComparison.OrdinalIgnoreCase ) )
				return true; // match
			else return false;
		}

		[XmlAttribute( "when" )] public string WhenString
		{
			get { return Format.DateToString( m_When, "r" ); }
			set { m_When = Format.StringToDate( value, "r" ); }
		}

		[XmlAttribute( "by" )] public string By
		{ get { return m_By; } set { m_By = value; } }

		#region Unserialized Properties

		[XmlIgnore] public DateTime When
		{ get { return m_When; } set { m_When = value; } }

		# endregion
	}
}
