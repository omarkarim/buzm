using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Core;
using NUnit.Util;

namespace Buzm.Utility
{
	public class NUnitHarness
	{
		private const string ASSEMBLY_PREFIX = "Buzm";
		private const string ASSEMBLY_SUFFIX = ".dll";
		private const string IGNORE_CATEGORY = "IgnoreNUnitHarness";

		public static string[] GetLocalAssemblies()
		{
			List<string> assemblies = new List<string>();
			Assembly rootAssembly = Assembly.GetEntryAssembly();

			string rootAssemblyPath = rootAssembly.CodeBase;
			assemblies.Add( Path.GetFileName( rootAssemblyPath ) );

			// iterate loaded assemblies and add those with local name prefix
			foreach( AssemblyName assembly in rootAssembly.GetReferencedAssemblies() )
			{
				if( assembly.Name.StartsWith( ASSEMBLY_PREFIX ) )
					assemblies.Add( assembly.Name + ASSEMBLY_SUFFIX );
			}
			return assemblies.ToArray();
		}

		/// <summary>Runs NUnit tests if debug flag is set 
		/// and redirects output to the log file </summary>
		public static void RunAllTests()
		{
			#if DEBUG
				Log.Write( "Starting tests", TraceLevel.Off, "NUnitHarness" );
				RunAllTestsInternal(); // run all tests in buzm assemblies 
			#else
				Log.Write( "Set debug flag", TraceLevel.Off, "NUnitHarness" );
			#endif
		}

		#region NUnit Runtime API Code
		#if DEBUG

		private static void RunAllTestsInternal()
		{			
			SimpleTestRunner runner = new SimpleTestRunner();
			Test test = runner.Load( "Buzm", GetLocalAssemblies() );

			TestResult result = runner.Run( new LogEventListener() );
			ResultSummarizer summary = new ResultSummarizer( result );

			Log.Write( "Finished " + summary.ResultCount + " tests in "
				+ summary.Time + " seconds. " + summary.Failures
				+ " tests failed and " + summary.TestsNotRun + " tests"
				+ " were ignored.", TraceLevel.Off, "NUnitHarness" );
		}

		public class LogEventListener : EventListener
		{
			public void TestStarted( TestCase testCase )
			{
				if( ( testCase.IgnoreReason != null ) ||
					( testCase.HasCategory( IGNORE_CATEGORY ) ) )
				{				
					Log.Write( "Ignoring " + testCase.FullName,
						TraceLevel.Info, "NUnitHarness" );
					testCase.ShouldRun = false;
				}
				else
				{
					Log.Write( "Running " + testCase.FullName,
					TraceLevel.Info, "NUnitHarness" );
				}
			}

			public void TestFinished( TestCaseResult result )
			{
				if( result.IsFailure ) // test failed
				{
					Log.Write( "Failed " + result.Name + ": " + result.Message,
					TraceLevel.Info, "NUnitHarness" );
				}
				else
				{
					Log.Write( "Finished " + result.Name + ": " + result.Time + " secs",
					TraceLevel.Info, "NUnitHarness" );
				}
			}

			public void TestOutput( TestOutput testOutput ) { }			
			public void RunFinished( Exception exception ) { }
			public void RunFinished( TestResult[] results ) { }
			public void RunStarted( Test[] tests ) { }
			public void SuiteFinished( TestSuiteResult result ) { }
			public void SuiteStarted( TestSuite suite ) { }
			public void UnhandledException( Exception exception ) { }
		}

		#endif
		#endregion
	}
}
