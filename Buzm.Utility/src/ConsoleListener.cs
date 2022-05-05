using System;
using System.Diagnostics;

namespace Buzm.Utility
{
	/// <summary>Redirects trace listener
	/// to the console. Can be used to send
	/// trace message to NUnit window</summary>
	public class ConsoleListener : TraceListener
	{
		public override void Write( string message ){ Console.Write( message ); }
		public override void WriteLine( string message ){ Console.WriteLine( message ); }
	}
}
