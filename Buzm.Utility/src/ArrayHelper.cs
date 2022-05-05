using System;
using System.Collections;
using NUnit.Framework;

namespace Buzm.Utility
{
	/// <summary> Provides static helper 
	/// functions for array manipulation </summary>
	public class ArrayHelper
	{
		/// <summary>Removes and returns the 
		/// first element of an array </summary>
		public static object Dequeue( ref Array array )
		{ 
			object obj = null;
			int length = array.Length;

			if( length > 0 ) // only remove if this is not an empty array
			{	
				obj = array.GetValue( 0 );
				Type type = array.GetType(); // dynamically create array of same type								
				Array newArray = Array.CreateInstance( type.GetElementType(), length - 1 );
				if( length > 1 ) Array.Copy( array, 1, newArray, 0, length - 1 );
				array = newArray;
			}
			return obj; // returns dequeued object or null if array is empty
		}

		/// <summary>Combines two arrays into one. The method is strongly 
		/// typed for better performance but could be made generic </summary>
		public static string[] Join( string[] arrayOne, string[] arrayTwo )
		{ 
			string[] joinArray = new String[arrayOne.Length + arrayTwo.Length];
			Array.Copy( arrayOne, 0, joinArray, 0, arrayOne.Length );
			Array.Copy( arrayTwo, 0, joinArray, arrayOne.Length, arrayTwo.Length );
			return joinArray;
		}

		/// <summary>Removes first instance of strings common to both arrays. The method 
		/// is strongly typed for better performance but could be made generic </summary>
		public static void RemoveDuplicates( ref string[] arrayOne, ref string[] arrayTwo )
		{ 			
			bool unique = true;
			int matchCount = 0; 
			bool[] arrayTwoMatchFlags = new bool[arrayTwo.Length];
			ArrayList listOneUnique = new ArrayList( arrayOne.Length );

			// search both arrays for duplicates
			for( int x=0; x < arrayOne.Length; x++ )
			{
				unique = true; // assume unique string
				for( int y=0; y < arrayTwo.Length; y++ )
				{	
					// if a duplicate is found
					if( arrayOne[x] == arrayTwo[y] )
					{	// if not previously found
						if( !arrayTwoMatchFlags[y] )
						{
							matchCount++;
							unique = false;
							arrayTwoMatchFlags[y] = true;
							break;
						}						
					}
				}
				// copy arrayOne unique strings to list
				if( unique ) listOneUnique.Add( arrayOne[x] ); 
			}
						
			// create a new array to hold the unique arrayTwo elements
			string[] arrayTwoUnique = new string[arrayTwo.Length - matchCount];			
			
			int flagCount = 0; // populate arrayTwoUnique
			for( int i=0; i < arrayTwoMatchFlags.Length; i++ )
			{
				if( arrayTwoMatchFlags[i] == false )
				{
					arrayTwoUnique[flagCount] = arrayTwo[i];
					flagCount++;
				}
			}
						
			arrayTwo = arrayTwoUnique; // set arg refs to unique arrays
			arrayOne = (string[])listOneUnique.ToArray( typeof(string) );		
		}

		/// <summary>Combines the elements of two arrays</summary>
		public static bool AreEqual( byte[] arrayOne, byte[] arrayTwo )
		{
			if( (arrayOne == null) && (arrayTwo == null) )
				return true; // since both are null

			if( (arrayOne != null) && (arrayTwo != null) )
			{
				if( arrayOne.Length == arrayTwo.Length )
				{
					for( int i = 0; i < arrayOne.Length; i++ )
					{
						if( arrayOne[i] != arrayTwo[i] )
							return false; // not equal
					}
					return true; // are equal
				}
			}
			return false; // not equal
		}
		
		#region NUnit Automated Test Cases

		[TestFixture] public class ArrayHelperTest
		{			
			string[] m_GuidArrayOne;
			string[] m_GuidArrayTwo;
			int GUID_ARRAY_LENGTH = 1000;

			[SetUp] public void SetUp()
			{ 
				string guid;
				m_GuidArrayOne = new string[GUID_ARRAY_LENGTH + 1];
				m_GuidArrayTwo = new string[GUID_ARRAY_LENGTH + 2];

				// add some different guids to array starts
				m_GuidArrayOne[0] = Guid.NewGuid().ToString();
				m_GuidArrayOne[1] = Guid.NewGuid().ToString();
				m_GuidArrayTwo[0] = Guid.NewGuid().ToString();
				m_GuidArrayTwo[1] = Guid.NewGuid().ToString();

				// add identical guids to array middles
				for( int i = 2; i < GUID_ARRAY_LENGTH; i++ )
				{
					guid = Guid.NewGuid().ToString();
					m_GuidArrayOne[i] = guid;
					m_GuidArrayTwo[i] = guid;
				}

				// add some different guids to array ends
				m_GuidArrayOne[GUID_ARRAY_LENGTH] = Guid.NewGuid().ToString();
				m_GuidArrayTwo[GUID_ARRAY_LENGTH] = Guid.NewGuid().ToString();
				m_GuidArrayTwo[GUID_ARRAY_LENGTH + 1] = Guid.NewGuid().ToString();
			}

			[TearDown] public void TearDown(){ }

			[Test] public void JoinTest()
			{
				string[] arrayOne = new string[0];
				string[] arrayTwo = new string[0];				
				string[] joinArray = ArrayHelper.Join( arrayOne, arrayTwo );

				// check to make sure joinArray is zero length
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 0, joinArray.Length );
				
				arrayOne = new string[]{ "5", "65" };
				arrayTwo = new string[]{ "10", "12", "15" };	
				joinArray = ArrayHelper.Join( arrayOne, arrayTwo );
				
				// check joinArray for appropriate values
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 5, joinArray.Length );
				Assertion.AssertEquals( "Incorrect array element at index zero.", "5", joinArray[0] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "65", joinArray[1] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "10", joinArray[2] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "12", joinArray[3] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "15", joinArray[4] );
			}

			[Test] public void RemoveDuplicatesTest()
			{
				string[] arrayOne = new string[]{ "1", "5", "10", "15" };
				string[] arrayTwo = new string[]{ "1", "3", "4", "5", "9" };				
				ArrayHelper.RemoveDuplicates( ref arrayOne, ref arrayTwo );

				// check arrayOne for appropriate values after duplicates have been removed
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 2, arrayOne.Length );
				Assertion.AssertEquals( "Incorrect array element at index zero.", "10", arrayOne[0] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "15", arrayOne[1] );

				// check arrayTwo for appropriate values after duplicates have been removed
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 3, arrayTwo.Length );
				Assertion.AssertEquals( "Incorrect array element at index zero.", "3", arrayTwo[0] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "4", arrayTwo[1] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "9", arrayTwo[2] );

				arrayOne = new string[]{ "5", "65", "10", "123", "12", "12", "65", "50" };
				arrayTwo = new string[]{ "1", "65", "10", "50", "9", "10", "12", "12" };		
				ArrayHelper.RemoveDuplicates( ref arrayOne, ref arrayTwo );

				// check arrayOne for appropriate values after duplicates have been removed
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 3, arrayOne.Length );
				Assertion.AssertEquals( "Incorrect array element at index zero.", "5", arrayOne[0] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "123", arrayOne[1] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "65", arrayOne[2] );

				// check arrayTwo for appropriate values after duplicates have been removed
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 3, arrayTwo.Length );
				Assertion.AssertEquals( "Incorrect array element at index zero.", "1", arrayTwo[0] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "9", arrayTwo[1] );
				Assertion.AssertEquals( "Incorrect array element at index one.", "10", arrayTwo[2] );
			}

			[Test] public void RemoveDuplicatesLoadTest()
			{
				// remove duplicates from guid array built at setup
				ArrayHelper.RemoveDuplicates( ref m_GuidArrayOne, ref m_GuidArrayTwo );

				// check both array for appropriate lengths after duplicates have been removed
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 3, m_GuidArrayOne.Length );
				Assertion.AssertEquals( "Incorrect array length after dequeue.", 4, m_GuidArrayTwo.Length );
			}
		}

		#endregion
	}
}
