using System;
using System.Xml.Schema;
using System.Xml.Serialization;
using NUnit.Framework;
using Buzm.Utility;

namespace Buzm.Schemas
{
	[XmlRoot( "user", Namespace = "", IsNullable = false )]
	public class UserConfigType : BaseType
	{
		protected string m_Guid;
		protected string m_Login;
		protected string m_Email;
		protected string m_Hives;
		protected string m_Friends;
		protected string m_MemberState;
		protected string m_RegisterDate;
		protected string m_LoginDate;
		protected string m_FirstName;
		protected string m_LastName;
		protected string m_Password;

		public UserConfigType() { }
		public UserConfigType( bool initFields )
		{
			// backward compatible init for code that
			if( initFields ) // expects empty nodes
			{
				m_Guid = String.Empty;
				m_Login = String.Empty;
				m_Email = String.Empty;
				m_Hives = String.Empty;
				m_Friends = String.Empty;
				m_MemberState = String.Empty;
				m_RegisterDate = String.Empty;
				m_LoginDate = String.Empty;
				m_FirstName = String.Empty;
				m_LastName = String.Empty;
				m_Password = String.Empty;					
			}
		}
		
		public bool IsValid() // check min config
		{
			return (!String.IsNullOrEmpty( m_Guid )
				&& (!String.IsNullOrEmpty( m_Email )
				||	!String.IsNullOrEmpty( m_Login )));			
		}

		[XmlElement( "guid", Form = XmlSchemaForm.Unqualified )]
		public string Guid { get { return m_Guid; } set { m_Guid = value; } }
		
		[XmlElement( "login", Form = XmlSchemaForm.Unqualified )]
		public string Login { get { return m_Login; } set { m_Login = value; } }
		
		[XmlElement( "email", Form = XmlSchemaForm.Unqualified )]
		public string Email { get { return m_Email; } set { m_Email = value; } }

		[XmlElement( "password", Form = XmlSchemaForm.Unqualified )]
		public string Password { get { return m_Password; } set { m_Password = value; } }

		[XmlElement( "firstName", Form = XmlSchemaForm.Unqualified )]
		public string FirstName { get { return m_FirstName; } set { m_FirstName = value; } }

		[XmlElement( "lastName", Form = XmlSchemaForm.Unqualified )]
		public string LastName { get { return m_LastName; } set { m_LastName = value; } }

		[XmlElement( "friends", Form = XmlSchemaForm.Unqualified )]
		public string Friends { get { return m_Friends; } set { m_Friends = value; } }

		[XmlElement( "hives", Form = XmlSchemaForm.Unqualified )]
		public string Hives { get { return m_Hives; } set { m_Hives = value; } }

		[XmlElement( "memberState", Form = XmlSchemaForm.Unqualified )]
		public string MemberState { get { return m_MemberState; } set { m_MemberState = value; } }

		[XmlElement( "registerDate", Form = XmlSchemaForm.Unqualified )]
		public string RegisterDate { get { return m_RegisterDate; } set { m_RegisterDate = value; } }

		[XmlElement( "loginDate", Form = XmlSchemaForm.Unqualified )]
		public string LoginDate { get { return m_LoginDate; } set { m_LoginDate = value; } }

		#region NUnit Automated Test Cases

		[TestFixture] public class UserConfigTypeTest
		{
			[SetUp] public void SetUp() { }
			[TearDown] public void TearDown() { }

			[Test] public void SerializeTest()
			{
				// set typical values for fields
				UserConfigType userConfig = new UserConfigType();
				userConfig.Guid = System.Guid.NewGuid().ToString();
				userConfig.Email = "okarim@buzm.com";
				userConfig.Login = String.Empty;

				string userConfigOutput = userConfig.ToXml();
				SafeXmlDoc userConfigXmlDoc = new SafeXmlDoc( userConfigOutput );

				Assert.AreEqual( userConfig.Guid, userConfigXmlDoc.GetInnerText( "/user/guid", "test" ), "Got incorrect legal guid" );
				Assert.AreEqual( userConfig.Email, userConfigXmlDoc.GetInnerText( "/user/email", "test" ), "Got incorrect legal email" );
				
				Assert.AreEqual( String.Empty, userConfigXmlDoc.GetInnerText( "/user/login", "test" ), "Got value for empty login" );
				Assert.IsNull( userConfigXmlDoc.SelectSingleNode( "/user/password" ), "Got value for null password" );
				
				// try to roundtrip messy guid value
				string guidValue = "\x0001<1>&u-me@buzm.cöm";
				string guidXml = "<user><guid></guid></user>";
				
				// set guid value in doc with chars to escape
				SafeXmlDoc guidXmlDoc = new SafeXmlDoc( guidXml );
				guidXmlDoc.SetInnerText( "/user/guid", guidValue, "test" );
				string xmlDocOutput = guidXmlDoc.ToString();

				// set same guid value in userConfig
				userConfig = new UserConfigType();
				userConfig.Guid = guidValue;
				
				// serialize userConfig obj to xml
				userConfigOutput = userConfig.ToXml();
				userConfigXmlDoc = new SafeXmlDoc( userConfigOutput );

				// compare roundtrip values using outer xml and inner text
				Assert.AreEqual( xmlDocOutput, userConfigOutput, "Got incorrect outer xml after serialization" );
				Assert.AreEqual( guidValue, userConfigXmlDoc.GetInnerText( "/user/guid", "test" ), "Got incorrect illegal guid" );								
			}

			[Test] public void IsValidTest()
			{
				UserConfigType userConfig = new UserConfigType();
				Assert.IsFalse( userConfig.IsValid(), "Null user config tested valid" );

				userConfig.Guid = String.Empty;
				Assert.IsFalse( userConfig.IsValid(), "Empty guid tested valid" );

				userConfig.Guid = "guid";
				Assert.IsFalse( userConfig.IsValid(), "Null login and email tested valid" );

				userConfig.Login = String.Empty;
				userConfig.Email = String.Empty;
				Assert.IsFalse( userConfig.IsValid(), "Empty login and email tested valid" );

				userConfig.Login = "login";
				Assert.IsTrue( userConfig.IsValid(), "Tested invalid with guid and login" );

				userConfig.Login = null;
				userConfig.Email = "email";
				Assert.IsTrue( userConfig.IsValid(), "Tested invalid with guid and email" );

				userConfig.Guid = null;
				Assert.IsFalse( userConfig.IsValid(), "Null guid tested valid" );
			}
		}

		#endregion
	}
}

