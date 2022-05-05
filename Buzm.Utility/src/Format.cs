using System;
using NUnit.Framework;
using System.Diagnostics;
using System.Globalization;

namespace Buzm.Utility
{
	public class Format
	{
		// compact and numerically sortable datetime format
		private const string DATETIME_FORMAT = "yyyyMMddHHmmss";
		
		public static string DateToString( DateTime dtm )
		{ 
			// convert to utc equivalent using global format
			return DateToString( dtm, DATETIME_FORMAT );
		}	

		public static string DateToString( DateTime dtm, string format )
		{
			// convert to utc equivalent using specified format
			return dtm.ToUniversalTime().ToString( format );
		}

		public static DateTime StringToDate( string defaultUtc )
		{
			// convert to local datetime using global format
			return StringToDate( defaultUtc, DATETIME_FORMAT );
		}

		public static DateTime StringToDate( string utc, string format )
		{ 
			try // converting utc string to local datetime
			{
				DateTime utcTime = DateTime.ParseExact( utc, 
					format, DateTimeFormatInfo.CurrentInfo );
				return utcTime.ToLocalTime(); // convert to system zone
			}
			catch( Exception e )
			{
				Log.Write( TraceLevel.Verbose, "Conversion failed", "Format.StringToDate", e );
				return DateTime.MinValue; // return uninitialized value
			}			
		}

		public string ToLocalDateString( string utc, string format )
		{
			try // converting buzm utc string to custom format
			{
				DateTime dtm = StringToDate( utc );
				return dtm.ToString( format );
			}
			catch ( Exception e )
			{
				Log.Write( TraceLevel.Verbose, "Invalid format", "Format.ToLocalDateString", e );
				return DateTime.Now.ToString(); // substitute current time
			}
		}

		public static string BooleanToString( bool value )
		{
			if( value ) return value.ToString().ToLower();
			else return null; // treat false as null
		}

		public static byte[] Base64ToBytes( string value, byte[] defaultBytes )
		{
			try // converting base64 string to byte array
			{
				return Convert.FromBase64String( value );
			}
			catch( Exception e )
			{
				Log.Write( TraceLevel.Warning, "Failed", "Format.Base64ToBytes", e );
				return defaultBytes; // conversion failed
			}
		}
				
		#region NUnit Automated Test Cases

		[TestFixture] public class FormatTest
		{				
			[SetUp] public void SetUp() { }
			[TearDown] public void TearDown(){ }

			[Test] public void DateTimeConvertTest()
			{
				DateTime fineDate = DateTime.Now.AddDays( 1 ); // contains nanosecond data
				DateTime userDate = new DateTime( fineDate.Year, fineDate.Month, fineDate.Day, 
												fineDate.Hour, fineDate.Minute, fineDate.Second );

				string encDateString = DateToString( fineDate ); // convert to utc string
				DateTime decDate = StringToDate( encDateString ); // convert back to local date
				Assert.AreEqual( userDate, decDate, "Date decoded incorrectly on first pass." );

				encDateString = DateToString( decDate ); // convert previously decoded date
				decDate = StringToDate( encDateString ); // to check for any accumulated errors
				Assert.AreEqual( userDate, decDate, "Date decoded incorrectly on second pass." );

				decDate = StringToDate( "blah blah" ); // invalid string should return empty date
				Assert.AreEqual( DateTime.MinValue, decDate, "Trap of invalid date string failed." );

				decDate = StringToDate( String.Empty ); // empty string should return empty date
				Assert.AreEqual( DateTime.MinValue, decDate, "Trap of empty date string failed." );

				Format dateFormat = new Format(); // create a format object for instance method tests
				string formatDateString = dateFormat.ToLocalDateString( null, null ); // invalid date

				decDate = DateTime.ParseExact( formatDateString, "G", DateTimeFormatInfo.CurrentInfo );
				Assert.AreEqual( DateTime.MinValue, decDate, "Trap of null date string failed." );

				formatDateString = dateFormat.ToLocalDateString( encDateString, "b" ); // invalid format
				decDate = DateTime.ParseExact( formatDateString, "G", DateTimeFormatInfo.CurrentInfo );
				Assert.AreEqual( DateTime.Now.Date, decDate.Date, "Trap of invalid date format failed." );

				formatDateString = dateFormat.ToLocalDateString( encDateString, "s" ); // sortable format
				decDate = DateTime.ParseExact( formatDateString, "s", DateTimeFormatInfo.CurrentInfo );
				Assert.AreEqual( userDate, decDate, "Date decoded incorrectly after format convert." );
			}

			[Test] public void DateTimeFormatTest()
			{
				DateTime nowDate = DateTime.Now;
				string defaultDateString = DateToString( nowDate );
				string formatDateString = DateToString( nowDate, "r" );

				DateTime defaultDate = StringToDate( defaultDateString );
				DateTime formatDate = StringToDate( formatDateString, "r" );

				Assert.AreEqual( defaultDate, formatDate, "Roundtrip with multiple formats failed" );
				Assert.AreEqual( nowDate.Date, formatDate.Date, "Roundtrip of custom format failed." );
			}
		}

		#endregion		
	}
}
