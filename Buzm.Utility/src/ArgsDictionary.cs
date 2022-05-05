using System.Collections.Specialized;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Buzm.Utility
{
	/// <summary>Parses a string array of command line 
	/// arguments into a string dictionary. Valid argument
	/// forms are {-,/,--}param{ ,=,:}(("')value('"))</summary>
	public class ArgsDictionary : StringDictionary
	{
		private bool m_RemoteArgs; // specifies if args were received from another process		
		private const string ARGS_REGEX = @"^([/-]|--){1}(?<name>\w+)([:=])?(?<value>.+)?$";
		// example args: -param1 "value 1" --param2 /param3="value 3" -param4 /param5

		public ArgsDictionary( string[] args )
		{
			m_RemoteArgs = false;
			string lastName = null;

			char[] trimChars = { '"', '\'' };
			Regex argsRegex = new Regex( ARGS_REGEX );
			
			foreach( string arg in args ) // iterate args
			{
				Match match = argsRegex.Match( arg );
				if( match.Success ) // if match found
				{
					lastName = match.Groups["name"].Value; // found param name
					if( !ContainsKey( lastName ) ) //  if param does not exist
					{
						// add the param and optionally a value to the dictionary 
						Add( lastName, match.Groups["value"].Value.Trim( trimChars ) );					
					}
				}
				else
				{
					// found a value for the last space separated nameval pair
					if( lastName != null ) this[lastName] = arg.Trim( trimChars );
				}
			}
		}
		
		/// <summary>True if args were received
		/// from a remote Buzm process </summary>
		public bool RemoteArgs
		{ 
			get { return m_RemoteArgs; }
			set { m_RemoteArgs = value; }
		}
	}

	#region NUnit Automated Test Cases

	[TestFixture] public class ArgsDictionaryTest
	{	
		[SetUp] public void SetUp() { }
		[TearDown] public void TearDown(){ }

		[Test] public void ParseArgumentsTest()
		{
			string[] args = new string[]{ @"C:\Buzm Invite.buz", "--test", "/test", 
			"\"hello\"", "-invite", @"C:\invite one\this", "/arg='value one'", "-t" };
			
			ArgsDictionary argsDict = new ArgsDictionary( args );
			Assertion.AssertEquals( "Got unexpected number of args", 4, argsDict.Count );
			Assertion.AssertEquals( "Got incorrect argument value", "hello", argsDict["test"] );
			Assertion.AssertEquals( "Got incorrect argument value", @"C:\invite one\this", argsDict["invite"] );
			Assertion.AssertEquals( "Got incorrect argument value", "value one", argsDict["arg"] );
			Assertion.Assert( "Did not find expected parameter 't'", argsDict.ContainsKey( "t" ) );
		}
	}

	#endregion
}

