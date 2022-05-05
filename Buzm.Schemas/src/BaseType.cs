using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Buzm.Utility;

namespace Buzm.Schemas
{
	/// <summary>Base class for most
	/// serializable xml types</summary>
	public abstract class BaseType
	{		
		private XmlDocument m_XmlDoc; 
		private Regex m_IllegalCharsRegex; 
		
		private const string CDATA_SUFFIX = "]]>";
		private const string CDATA_PREFIX = "<![CDATA[";
		
		public BaseType()
		{
			m_XmlDoc = new XmlDocument(); // used to create cdata elements
			m_IllegalCharsRegex = new Regex( "[\x00-\x08\x0b\x0c\x0e\x0f\x10-\x1f]" );
		}

		public virtual XmlSerializerNamespaces GetNamespaces()
		{
			XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
			ns.Add( "", "" ); // clear default namespaces
			return ns; // subclasses add as needed
		}

		protected XmlElement CreateCDataElement( string name, string data )
		{
			try // to create a cdata section
			{	
				if( data == null ) return null;
				else // only create element if data exists
				{
					XmlElement element = m_XmlDoc.CreateElement( name );
					string elemData = RemoveIllegalChars( RemoveCDataMarkup(data) );
					XmlCDataSection cdata = m_XmlDoc.CreateCDataSection( elemData );
					element.AppendChild( cdata );
					return element;
				}
			}
			catch( Exception e )
			{
				Log.Write( "Could not create cdata section: " + name, 
				TraceLevel.Warning, "BaseType.CreateCDataElement", e );
				return null; 
			}
		}

		private string RemoveCDataMarkup( string data )
		{			
			string sansPrefix = data.Replace( CDATA_PREFIX, "" );
			return sansPrefix.Replace( CDATA_SUFFIX, "" );			
		}
		
		protected string RemoveIllegalChars( string data )
		{
			if( data == null ) return null; // ignore field
			else return m_IllegalCharsRegex.Replace( data, "" );
		}

		public string ToXml()
		{
			try // serializing xml type to string
			{
				StringWriter strWriter = new StringWriter();
				XmlFragmentWriter xmlWriter = new XmlFragmentWriter( strWriter );
				
				XmlSerializer serializer = new XmlSerializer( this.GetType() );			
				serializer.Serialize( xmlWriter, this, GetNamespaces() );

				return strWriter.ToString(); // return xml
			}
			catch( Exception e ) 
			{
				Log.Write( "Xml serialization failed: " + this.GetType().Name, 
				TraceLevel.Warning, "BaseType.ToString", e );
				return String.Empty; 
			}
		}

		public static T FromXml<T> ( string xml )
		{
			try // creating type from xml string
			{
				StringReader strReader = new StringReader( xml );
				XmlSerializer serializer = new XmlSerializer( typeof(T) );
				return (T) serializer.Deserialize( strReader );
			}
			catch( Exception e )
			{
				Log.Write( "Deserialization failed: " + typeof(T).Name,
				TraceLevel.Warning, "BaseType.FromXml", e );
				return default( T ); // return null
			}
		}
	}
}
