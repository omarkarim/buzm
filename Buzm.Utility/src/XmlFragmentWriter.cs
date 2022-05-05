using System.IO;
using System.Xml;

namespace Buzm.Utility
{
	/// <summary>Writes xml without declaration</summary>
	public class XmlFragmentWriter : XmlTextWriter
	{
		public XmlFragmentWriter( TextWriter writer ) : base( writer ){}
		public override void WriteStartDocument( bool standalone ){}
		public override void WriteStartDocument( ){}
	}
}
