using System;
using System.Diagnostics;

namespace Buzm.Utility
{
	public class Log
	{
		private static TraceLevel m_TraceLevel;

		static Log()
		{
			try // to initialize log file specified in configuration
			{ 
				// determine the required trace level from the config file
				TraceSwitch defaultSwitch = new TraceSwitch( "default", "" );
				m_TraceLevel = defaultSwitch.Level; 

				// insert a message to initiate trace logging
				Write( TraceLevel.Off, "Trace initializing.", "Log" );
				Trace.Indent(); // indent log file to mark a new session
			}
			catch{ /* log file setup might throw an error if file is locked */ }
		}

		/// <summary> Write to the log </summary>
		public static void Write( string message )
		{ 
			Write( message, TraceLevel.Info, "Unknown" );
		}

		/// <summary> Writes a message to the log. See other overloads for
		/// method signatures that support various code layout options </summary>
		public static void Write( TraceLevel level, string message, string source, Exception e )
		{
			Write( message, level, source, e );
		}

		/// <summary> Writes a message to the log. See other overloads for
		/// method signatures that support various code layout options </summary>
		public static void Write( TraceLevel level, string message, string source )
		{
			Write( message, level, source );
		}

		/// <summary> Writes a message to the log. See other overloads for
		/// method signatures that support various code layout options </summary>
		public static void Write( string message, TraceLevel level, string source, Exception e )
		{
			Write( message + " : Exception - " + e.ToString(), level, source );
		}

		/// <summary> Writes message to all registered Trace listeners if the 
		/// input tracelevel is less than or equal to the configured one</summary>		
		public static void Write( string message, TraceLevel level, string source )
		{ 
			try // writing to listeners
			{	
				if( m_TraceLevel >= level )
				{	Trace.WriteLine( "[" + DateTime.Now.ToString() + "] " + source + ": " + message ); }
			} 
			catch { /* Ignore any log write failures */ }			
		}

		public static TraceLevel TraceLevel
		{
			get { return m_TraceLevel; }
			set { m_TraceLevel = value; }
		}
	}
}
