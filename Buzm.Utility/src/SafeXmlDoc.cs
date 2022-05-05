using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Text;
using NUnit.Framework;
using System.Diagnostics;
using System.Collections.Generic;

namespace Buzm.Utility
{
	/// <summary>Provides an exception safe
	/// wrapper around XmlDocument. Will add
	/// thread-safety in the future </summary>
	public class SafeXmlDoc : XmlDocument
	{
		public SafeXmlDoc() : base(){ }

		public SafeXmlDoc( string xml ) : base()
		{ 
			// load provided xml into the document
			LoadFromString( xml, "SafeXmlDoc.Init" );
		}

		public bool LoadFromString( string xml, string logMsg )
		{
			try // loading xml document 
			{ 
				LoadXml( xml ); 
				return true;
			}
			catch( Exception e )
			{ 
				Log.Write( "Could not load xml doc - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.LoadFromString", e );
				return false;
			}
		}

		public bool LoadFromFile( string filename, string logMsg )
		{
			try // loading xml document 
			{ 
				Load( filename ); 
				return true;
			}
			catch( Exception e )
			{ 
				Log.Write( "Could not load: " + filename + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.LoadFromFile", e );
				return false;
			}
		}

		public bool LoadFromWeb( string uri, string logMsg )
		{
			try // loading xml document from the Web
			{
				WebRequest request = WebRequest.Create( uri );
				return LoadFromWeb( request, uri + " - " + logMsg );
			}
			catch( Exception e )
			{
				Log.Write( "Could not load: " + uri + " - " + logMsg,
				TraceLevel.Info, "SafeXmlDoc.LoadFromWeb", e );
				return false;
			}
		}

		public bool LoadFromWeb( WebRequest request, string logMsg )
		{
			WebResponse response = null;
			Stream responseStream = null;
			
			try // loading web xml document
			{
				response = request.GetResponse();
				responseStream = response.GetResponseStream();
				
				Load( responseStream );
				return true;
			}
			catch( Exception e )
			{
				Log.Write( "Could not load request from Web - " + logMsg,
				TraceLevel.Info, "SafeXmlDoc.LoadFromWeb", e );
			}
			finally
			{
				try // to release resources
				{
					if( responseStream != null ) responseStream.Close();
					if( response != null ) response.Close();
				}
				catch { /* ignore */ }
			}
			return false;
		}

		public bool SaveToFile( string filename, string logMsg )
		{
			try // saving xml to disk
			{
				Save( filename );
				return true;
			}
			catch( Exception e )
			{
				Log.Write( "Could not write: " + filename + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.SaveToFile", e );
				return false;
			}
		}

		public bool SaveToFile( string filename, Encoding encoding, string logMsg )
		{
			try // saving encoded xml file to disk
			{
				XmlTextWriter writer = new XmlTextWriter( filename, encoding );
				writer.Formatting = Formatting.Indented;
				Save( writer ); // write encoded xml 
				writer.Flush(); writer.Close();
				return true;
			}
			catch( Exception e )
			{
				Log.Write( "Could not write encoded: " + filename + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.SaveToFile", e );
				return false;
			}
		}

		public XmlNode GetNode( string xpath, string logMsg )
		{
			// return node at given xpath location
			try { return SelectSingleNode( xpath ); }
			catch( Exception e )
			{
				Log.Write( "Get failed: " + xpath + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.GetNode", e );
				return null; 
			}
		}

		public XmlNodeList GetNodes( string xpath, string logMsg )
		{
			// return nodes at xpath location
			try { return SelectNodes( xpath ); }
			catch( Exception e )
			{
				Log.Write( "Get failed: " + xpath + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.GetNodes", e );
				return null;
			}
		}

		public string GetOuterXml( string xpath, string logMsg )
		{
			try // getting outer xml
			{
				XmlNode node = SelectSingleNode( xpath );
				if( node != null ) return node.OuterXml;
			}
			catch( Exception e )
			{
				Log.Write( "Get failed: " + xpath + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.GetOuterXml", e );
			}
			return ""; // return empty string if no outer xml
		}


		public string GetInnerText( string xpath, string logMsg )
		{
			try // getting inner text
			{
				XmlNode node = SelectSingleNode( xpath );
				if( node != null ) return node.InnerText;
			}
			catch( Exception e )
			{
				Log.Write( "Get failed: " + xpath + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.GetInnerText", e );
			}
			return ""; // return empty string if no inner text
		}

		// TODO: CDATA inner text settings automatically to hide escape chars
		public bool SetInnerText( string xpath, string text, string logMsg )
		{
			try // setting inner text
			{
				XmlNode node = SelectSingleNode( xpath );
				if( node != null )
				{	
					node.InnerText = text;
					return true;
				}
				else return false;
			}
			catch( Exception e )
			{
				Log.Write( "Set failed: " + xpath + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.SetInnerText", e );
				return false; 
			}
		}

		/// <summary>Retrieves the first node within the parent's set 
		/// containing a 'guid' element with the specified value </summary>
		/// <param name="parent">Node to search for children within</param>
		/// <param name="childGuid">The value for the guid element of the child</param>
		/// <param name="logMsg">Suffix that will be added to error log entries</param>
		/// <returns>The matching child node if one was found, otherwise null</returns>
		public XmlNode GetUniqueChild( XmlNode parent, string childGuid, string logMsg )
		{
			try // retreiving the child node with matching guid
			{
				string existsQuery = "*[guid='" + childGuid + "']";
				return parent.SelectSingleNode( existsQuery );
			}
			catch( Exception e )
			{
				Log.Write( "Error while matching guid " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.GetUniqueChild", e );
				return null; // indicate that no matching child found
			}
		}

		public XmlNode GetUniqueChild( string parentPath, string childGuid, string logMsg )
		{
			XmlNode parent = GetNode( parentPath, logMsg );
			if( parent!= null ) return GetUniqueChild( parent, childGuid, logMsg );
			else return null; // obviously no unique child for non-existent parent
		}

		/// <summary>Appends or updates childXml within the parent node. 
		/// The 'guid' element of the child is used to determine if it is 
		/// unique. The child is updated if its guid exists already</summary>
		/// <returns>Child node if set is successful, otherwise returns null</returns>
		public XmlNode SetUniqueChild( XmlNode parent, string childXml, string logMsg )
		{
			try  // setting unique child based on guid element
			{
				// create a new document fragment to hold child xml
				XmlDocumentFragment newChild = CreateDocumentFragment();
				newChild.InnerXml = childXml; // populate the new fragment
				
				XmlNode guid = newChild.SelectSingleNode( "/*/guid" );
				if( guid != null ) // if the child node has a guid 
				{					
					// check if a matching child already exists within the parent 
					XmlNode oldChild = GetUniqueChild( parent, guid.InnerText, logMsg );
					if( oldChild == null ) return parent.AppendChild( newChild );
					else return parent.ReplaceChild( newChild, oldChild );
				}
				else throw new XmlException( "Child does not contain a Guid element" );
			}
			catch( Exception e )
			{
				Log.Write( "Error while setting new child" + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.SetUniqueChild", e );
				return null; // indicate that child could not be set 
			}
		}

		public XmlNode SetUniqueChild( string parentPath, string childXml, string logMsg )
		{
			XmlNode parent = GetNode( parentPath, logMsg );
			if( parent!= null ) return SetUniqueChild( parent, childXml, logMsg );
			else return null; // cannot set unique child for non-existent parent
		}

		public XmlNode SetUniqueChild( string childXml, string logMsg )
		{
			XmlNode root = DocumentElement; // retrieve document root node
			if( root != null ) return SetUniqueChild( root, childXml, logMsg );
			else return null; // cannot set unique child for empty document
		}

		/// <summary>Removes the first node within the parent's set 
		/// containing a 'guid' element with the specified value </summary>
		/// <param name="parent">Node to search for children within</param>
		/// <param name="childGuid">The value for the guid element of the child</param>
		/// <param name="logMsg">Suffix that will be added to error log entries</param>
		/// <returns>The removed child node if one was matched, otherwise null</returns>
		public XmlNode RemoveUniqueChild( XmlNode parent, string childGuid, string logMsg )
		{
			try  // removing unique child based on guid element
			{
				// check if a matching child exists within the parent 
				XmlNode child = GetUniqueChild( parent, childGuid, logMsg );
				if( child != null ) return parent.RemoveChild( child );
				else return null; // since child was not found
			}
			catch( Exception e )
			{
				Log.Write( "Error while removing child" + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.RemoveUniqueChild", e );
				return null; // indicate that child was not removed
			}
		}

		public XmlNode RemoveUniqueChild( string parentPath, string childGuid, string logMsg )
		{
			XmlNode parent = GetNode( parentPath, logMsg );
			if( parent!= null ) return RemoveUniqueChild( parent, childGuid, logMsg );
			else return null; // cannot remove child from non-existent parent
		}

		public XmlNode RemoveUniqueChild( string childGuid, string logMsg )
		{
			XmlNode root = DocumentElement; // retrieve document root node
			if( root != null ) return RemoveUniqueChild( root, childGuid, logMsg );
			else return null; // cannot remove unique child from empty document
		}

		public override string ToString()
		{
			if( DocumentElement != null ) return DocumentElement.OuterXml;
			else return String.Empty; // the document is not yet populated
		}

		public static string GetText( XmlNode node, string xpath, string logMsg )
		{
			try // getting inner text for node child
			{
				XmlNode child = node.SelectSingleNode( xpath );
				if( child != null ) return child.InnerText;
			}
			catch( Exception e )
			{
				Log.Write( "Get failed: " + xpath + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.GetText", e );
			}
			return String.Empty; // if all else fails
		}

		public static bool SetText( XmlNode node, string xpath, string text, string logMsg )
		{
			try // setting inner text for node child
			{
				XmlNode child = node.SelectSingleNode( xpath );
				if( child != null )
				{
					child.InnerText = text;
					return true;
				}
			}
			catch( Exception e )
			{
				Log.Write( "Set failed: " + xpath + " - " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.SetText", e );				
			}
			return false; // if code reached here
		}

		public static string[] GetNodeGuids( XmlNodeList nodes, string logMsg )
		{
			try // to get guids for nodes that have them
			{
				List<string> guids = new List<string>();
				foreach( XmlNode node in nodes )
				{
					XmlNode guidNode = node.SelectSingleNode( "guid" );
					if( guidNode != null ) // guid node exists
					{
						string guid = guidNode.InnerText;
						if( guid != String.Empty ) guids.Add( guid );
					}
				}
				return guids.ToArray();
			}
			catch( Exception e )
			{
				Log.Write( "Could not get guids: " + logMsg,
				TraceLevel.Warning, "SafeXmlDoc.GetNodeGuids", e );
			}
			return new string[0]; // no guids
		}

		#region NUnit Automated Test Cases

		[TestFixture] public class SafeXmlDocTest
		{
			[SetUp] public void SetUp(){ }
			[TearDown] public void TearDown() { }
			
			[Test] public void XmlLoadAndSaveTest()
			{
				string testValue;
				SafeXmlDoc xmlFileDoc = new SafeXmlDoc();
				SafeXmlDoc xmlStringDoc = new SafeXmlDoc();
				
				string xml = "<buzm><test>testvalue</test></buzm>";
				string xmlFilePath = Path.GetTempPath() + @"\" + "buzm_test.xml";

				xmlStringDoc.LoadFromString( xml, "NUnit test" );
				testValue = xmlStringDoc.SelectSingleNode("/buzm/test").InnerText;
				Assertion.AssertEquals( "Got incorrect xml value", "testvalue", testValue );

				// save file with default encoding
				xmlStringDoc.SaveToFile( xmlFilePath, "NUnit test" );
				xmlFileDoc.LoadFromFile( xmlFilePath, "NUnit test" );

				// check default encoded file for data
				testValue = xmlFileDoc.SelectSingleNode("/buzm/test").InnerText;
				Assertion.AssertEquals( "Got incorrect xml value", "testvalue", testValue );

				// save file with specific encoding
				xmlStringDoc.SaveToFile( xmlFilePath, Encoding.UTF8, "NUnit test" );
				xmlFileDoc.LoadFromFile( xmlFilePath, "NUnit test" );
				
				// check file for encoding
				XmlDeclaration xmlDec = (XmlDeclaration)xmlFileDoc.FirstChild;
				Assertion.AssertEquals( "Got incorrect encoding attribute", "utf-8", xmlDec.Encoding );

				// check encoded file for data
				testValue = xmlFileDoc.SelectSingleNode("/buzm/test").InnerText;
				Assertion.AssertEquals( "Got incorrect encoded xml value", "testvalue", testValue );

				// save empty file with encoding
				xmlStringDoc = new SafeXmlDoc( "" );
				xmlStringDoc.SaveToFile( xmlFilePath, Encoding.UTF8, "NUnit test" );
				xmlFileDoc.LoadFromFile( xmlFilePath, "NUnit test" );				
				Assertion.AssertEquals( "Got incorrect empty doc", "", xmlFileDoc.OuterXml );

				// Remove temp test file
				File.Delete( xmlFilePath );
			}

			[Test] public void ToStringTest()
			{
				SafeXmlDoc xmlDoc = new SafeXmlDoc();
				string xml = "<buzm><test>testvalue</test></buzm>";
				
				// try unpopulated doc
				Assertion.AssertEquals( "Got string from empty xml doc", "", xmlDoc.ToString( ) );

				// try doc with data loaded into it
				xmlDoc.LoadFromString( "<?xml version=\"1.0\"?>" + xml, "NUnit test" );
				Assertion.AssertEquals( "Got string from loaded xml doc", xml, xmlDoc.ToString( ) );
			}

			[Test] public void GetAndSetInnerTextTest()
			{
				bool setResult;
				string getResult;
				SafeXmlDoc xmlDoc = new SafeXmlDoc();
				string xml = "<buzm><test>testvalue</test></buzm>";
				
				// try setting text in unpopulated doc
				setResult = xmlDoc.SetInnerText( "/buzm/test", "buzmtest", "NUnitTest" );
				Assertion.Assert( "Set text in empty xml doc", !setResult );

				// try getting text from unpopulated doc
				getResult = xmlDoc.GetInnerText( "/buzm/test", "NUnitTest" );
				Assertion.AssertEquals( "Get text from empty xml doc", "", getResult );

				// try setting text with data loaded 
				xmlDoc.LoadFromString( xml, "NUnit test" );
				setResult = xmlDoc.SetInnerText( "/buzm/test", "bee", "NUnitTest" );
				Assertion.Assert( "Set text in populated xml doc", setResult );

				// try getting text with data loaded 
				getResult = xmlDoc.GetInnerText( "/buzm/test", "NUnitTest" );
				Assertion.AssertEquals( "Get text from populated xml doc", "bee", getResult );

				// try setting text in with invalid xpath query
				setResult = xmlDoc.SetInnerText( "/buzm/missing", "buzmtest", "NUnitTest" );
				Assertion.Assert( "Set text with invalid xpath query", !setResult );

				// try getting text from unpopulated doc
				getResult = xmlDoc.GetInnerText( "/buzm/missing", "NUnitTest" );
				Assertion.AssertEquals( "Get text with invalid xpath query", "", getResult );
			}

			[Test] public void GetOuterXmlTest()
			{
				string getResult;
				SafeXmlDoc xmlDoc = new SafeXmlDoc();
				string xml = "<buzm><test>bee</test></buzm>";
				
				// try getting text from unpopulated doc
				getResult = xmlDoc.GetOuterXml( "/buzm/test", "NUnitTest" );
				Assertion.AssertEquals( "Get xml from empty doc", "", getResult );

				// try getting text with data loaded  
				xmlDoc.LoadFromString( xml, "NUnit test" );
				getResult = xmlDoc.GetOuterXml( "/buzm/test", "NUnitTest" );
				Assertion.AssertEquals( "Get outer xml from populated doc", "<test>bee</test>", getResult );
			}

			[Test] public void GetNodesTest()
			{
				XmlNodeList getResult;
				SafeXmlDoc xmlDoc = new SafeXmlDoc();
				string[] values = new string[]{ "one", "two" };
				string xml = "<buzm><test>" + values[0] + "</test>"
					+ "<test>" + values[1] + "</test></buzm>";
				
				// try getting nodes from unpopulated doc
				getResult = xmlDoc.GetNodes( "/buzm/test", "NUnitTest" );
				Assertion.AssertEquals( "Get xml from empty doc", 0, getResult.Count );

				// load xml into document
				xmlDoc.LoadFromString( xml, "NUnit test" );
				
				// try getting nodes from invalid path
				getResult = xmlDoc.GetNodes( "/buzm/testing", "NUnitTest" );
				Assertion.AssertEquals( "Get xml from invalid path", 0, getResult.Count );
				
				// try getting nodes from valid path
				getResult = xmlDoc.GetNodes( "/buzm/test", "NUnitTest" );
				
				int counter = 0; // node counter
				foreach( XmlNode node in getResult )
				{
					Assertion.AssertEquals( "Get inner text from xml node list", values[counter], node.InnerText );
					counter++; // increment counter to the next xml value
				}
				
			}

			[Test] public void GetNodeTest()
			{
				XmlNode getResult;
				SafeXmlDoc xmlDoc = new SafeXmlDoc();
				string xml = "<buzm><test>bee</test></buzm>";
				
				// try getting node from unpopulated doc
				getResult = xmlDoc.GetNode( "/buzm/test", "NUnitTest" );
				Assertion.AssertNull( "Get node from empty doc", getResult );

				// load xml into document
				xmlDoc.LoadFromString( xml, "NUnit test" );
				
				// try getting node from invalid path
				getResult = xmlDoc.GetNode( "/buzm/testing", "NUnitTest" );
				Assertion.AssertNull( "Get node from invalid path", getResult );

				// try getting node from invalid prefix
				getResult = xmlDoc.GetNode( "/buzm/pref:test", "NUnitTest" );
				Assertion.AssertNull( "Get node from invalid prefix", getResult );
				
				// try getting nodes from valid path
				getResult = xmlDoc.GetNode( "/buzm/test", "NUnitTest" );
				Assertion.AssertEquals( "Get inner text from xml node", "bee", getResult.InnerText );			
			}

			[Test] public void GetUniqueChildTest()
			{
				XmlNode getResult;
				SafeXmlDoc xmlDoc = new SafeXmlDoc();
				string xml = "<buzm><child><name>joe</name></child></buzm>";
				
				// try getting child from unpopulated doc
				getResult = xmlDoc.GetUniqueChild( "/buzm", "1", "NUnitTest" );
				Assertion.AssertNull( "Get child from empty doc", getResult );

				// load xml into document
				xmlDoc.LoadFromString( xml, "NUnit test" );
				
				// try getting child from invalid path
				getResult = xmlDoc.GetUniqueChild( "/buzmer", "1", "NUnitTest" );
				Assertion.AssertNull( "Get child from invalid path", getResult );

				// try getting child without guid
				getResult = xmlDoc.GetUniqueChild( "/buzm", "1", "NUnitTest" );
				Assertion.AssertNull( "Get child without guid", getResult );

				// add guid to xml
				xml = "<buzm><child><name>joe</name><guid>1</guid></child></buzm>";
				xmlDoc.LoadFromString( xml, "NUnit test" );
				
				// try getting child with incorrect guid
				getResult = xmlDoc.GetUniqueChild( "/buzm", "0", "NUnitTest" );
				Assertion.AssertNull( "Get child with incorrect guid", getResult );

				// try getting child with correct guid
				getResult = xmlDoc.GetUniqueChild( "/buzm", "1", "NUnitTest" );
				string childName = getResult.SelectSingleNode( "name" ).InnerText;
				Assertion.AssertEquals( "Get child with correct guid", "joe", childName );		
	
				// add duplicate guids to xml
				xml = "<buzm><child><name>joe</name><guid>1</guid></child><child><name>mary</name><guid>1</guid></child></buzm>";
				xmlDoc.LoadFromString( xml, "NUnit test" );

				// try getting first child with correct guid
				getResult = xmlDoc.GetUniqueChild( "/buzm", "1", "NUnitTest" );
				childName = getResult.SelectSingleNode( "name" ).InnerText;
				Assertion.AssertEquals( "Get child with correct guid", "joe", childName );	
			}
	
			[Test] public void SetUniqueChildTest()
			{
				XmlNode setResult;
				string xml = "<buzm></buzm>";
				SafeXmlDoc xmlDoc = new SafeXmlDoc();			
				string childXml = "<child><name>joe</name></child>";
					
				// try setting child from unpopulated doc
				setResult = xmlDoc.SetUniqueChild( "/buzm", childXml, "NUnitTest" );
				Assertion.AssertNull( "Set child from empty doc", setResult );

				// try setting with document element from unpopulated doc
				setResult = xmlDoc.SetUniqueChild( childXml, "NUnitTest" );
				Assertion.AssertNull( "Set child from empty doc", setResult );

				// load xml into document
				xmlDoc.LoadFromString( xml, "NUnit test" );
					
				// try setting child from invalid path
				setResult = xmlDoc.SetUniqueChild( "/buzmer", childXml, "NUnitTest" );
				Assertion.AssertNull( "Set child from invalid path", setResult );

				// try setting child without guid
				setResult = xmlDoc.SetUniqueChild( "/buzm", childXml, "NUnitTest" );
				Assertion.AssertNull( "Set child without guid", setResult );

				// add guid to child xml
				childXml = "<child><name>joe</name><guid>1</guid></child>";
				
				// try setting child with guid
				setResult = xmlDoc.SetUniqueChild( "/buzm", childXml, "NUnitTest" );
				string childName = setResult.SelectSingleNode( "name" ).InnerText;
				Assertion.AssertEquals( "Set child with correct guid", "joe", childName );			

				// add another child and check result
				string newChildXml = "<child><name>mary</name><guid>2</guid></child>";
				setResult = xmlDoc.SetUniqueChild( "/buzm", newChildXml, "NUnitTest" );
				childName = setResult.SelectSingleNode( "name" ).InnerText;
				Assertion.AssertEquals( "Set another child with correct guid", "mary", childName );

				// add another child and check result
				string thirdChildXml = "<child><name>adam</name><guid>3</guid></child>";
				setResult = xmlDoc.SetUniqueChild( thirdChildXml, "NUnitTest" );
				childName = setResult.SelectSingleNode( "name" ).InnerText;
				Assertion.AssertEquals( "Set yet another child with correct guid", "adam", childName );


				//update existing child xml with new data and check entire doc
				string updatedChildXml = "<child><name>joe</name><age>12</age><guid>1</guid></child>";
				setResult = xmlDoc.SetUniqueChild( "/buzm", updatedChildXml, "NUnitTest" );
				string expectedXml = "<buzm>" + updatedChildXml + newChildXml + thirdChildXml + "</buzm>";
				Assertion.AssertEquals( "Updated child xml doc", expectedXml, xmlDoc.OuterXml );
			}

			[Test] public void RemoveUniqueChildTest()
			{
				XmlNode removeResult;
				string childGuid = "0";
				string xml = "<buzm></buzm>";
				SafeXmlDoc xmlDoc = new SafeXmlDoc();				
					
				// try removing child from unpopulated doc
				removeResult = xmlDoc.RemoveUniqueChild( "/buzm", childGuid, "NUnitTest" );
				Assert.IsNull( removeResult, "Remove child from empty doc" );

				// try setting with document element from unpopulated doc
				removeResult = xmlDoc.RemoveUniqueChild( childGuid, "NUnitTest" );
				Assert.IsNull( removeResult, "Remove child from empty doc" );

				// load xml into document
				xmlDoc.LoadFromString( xml, "NUnit test" );
					
				// try removing child from invalid path
				removeResult = xmlDoc.RemoveUniqueChild( "/buzmer", childGuid, "NUnitTest" );
				Assert.IsNull( removeResult, "Remove child from invalid path" );

				// try removing child without guid
				removeResult = xmlDoc.RemoveUniqueChild( "/buzm", childGuid, "NUnitTest" );
				Assert.IsNull( removeResult, "Remove child without guid" );

				// create child with appropriate guid
				string childXml = "<child><name>joe</name><guid>" + childGuid + "</guid></child>";
				XmlNode childNode = xmlDoc.SetUniqueChild( "/buzm", childXml, "NUnitTest" );

				// try to remove new child from xml doc
				removeResult = xmlDoc.RemoveUniqueChild( childGuid, "NUnitTest" );
				Assert.AreSame( childNode, removeResult, "Compare removed child with original" );
				Assert.IsNull( removeResult.ParentNode, "Parent of removed node should not exist" );

				// create xml doc with two children with the same guid
				xml = "<buzm>"
					  + "<child><name>joe</name><guid>" + childGuid + "</guid></child>"
					  + "<child><name>mary</name><guid>" + childGuid + "</guid></child>" +
					  "</buzm>";
				xmlDoc.LoadFromString( xml, "NUnit test" );

				// get should return first child
				childNode = xmlDoc.GetUniqueChild( "/buzm", childGuid, "NUnitTest" );
				string childName = childNode.SelectSingleNode( "name" ).InnerText;
				Assert.AreEqual( "joe", childName, "Incorrect name for first child" );	

				// try to remove first child from xml doc
				removeResult = xmlDoc.RemoveUniqueChild( "/buzm", childGuid, "NUnitTest" );
				Assert.AreSame( childNode, removeResult, "Compare removed node with first child" );
				Assert.IsNull( removeResult.ParentNode, "Parent of removed node should not exist" );

				// get should now return second child
				childNode = xmlDoc.GetUniqueChild( "/buzm", childGuid, "NUnitTest" );
				childName = childNode.SelectSingleNode( "name" ).InnerText;
				Assert.AreEqual( "mary", childName, "Incorrect name for second child" );	
			}
		}

		#endregion
	}
}
