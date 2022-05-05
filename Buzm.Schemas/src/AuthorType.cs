using System.Xml.Schema;
using System.Xml.Serialization;

namespace Buzm.Schemas
{
	[XmlRootAttribute( "author", Namespace="", IsNullable=false )]
	public class AuthorType
	{   
		[XmlElement( "login", Form=XmlSchemaForm.Unqualified )]
		public string Login;
	}
}
