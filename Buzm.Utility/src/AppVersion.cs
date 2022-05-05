using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.Windows.Forms;
using NUnit.Framework;

namespace Buzm.Utility
{
	/// <summary>Stores version information and provides methods to
	/// check if version is compatible with configured settings. The
	/// version is immutable to allow ToString output caching</summary>
	public class AppVersion
	{
		private string m_VersionString; // to string cache
		private Version m_CurrentVersion; // of this client
		private VersionSupportInfo[] m_VersionSupportInfoset;				
		
		private const string CONFIG_BASE_PATH = "versionSupport/";
		private const string VERSION_MARKER_FORMAT = "version-{0}.dat";

		public AppVersion() : this( Application.ProductVersion ){}
		public AppVersion( string version ) 
		{
			m_VersionSupportInfoset = LoadSupportInfo();
			try { m_CurrentVersion = new Version( version ); }
			catch { m_CurrentVersion = new Version(); } // v0.0
			m_VersionString = m_CurrentVersion.ToString();
		}

		private VersionSupportInfo[] LoadSupportInfo()
		{
			ArrayList supInfoList = new ArrayList();
			Type supType = typeof( VersionSupport );
	
			foreach( int supValue in Enum.GetValues( supType ) ) 
			{
				try // loading version support info from app config
				{
					string supName = Enum.GetName( supType, supValue );
					string supConfigPath = CONFIG_BASE_PATH + supName.ToLower();
					
					string version = Config.GetValue( supConfigPath + "/version" );
					if( ( version != null ) && ( version != String.Empty ) )
					{
						VersionSupportInfo supInfo = new VersionSupportInfo();			
						supInfo.Support = (VersionSupport)supValue; // cast enum
						
						supInfo.Link = Config.GetValue( supConfigPath + "/link" );
						supInfo.Message = Config.GetValue( supConfigPath + "/message" );

						supInfo.Version = new Version( version ); // parse version
						supInfoList.Add( supInfo ); // add valid support info to list
					}
				}
				catch { /* ignore string parsing errors thrown by Version constructor */ }
			}
			return (VersionSupportInfo[])supInfoList.ToArray( typeof( VersionSupportInfo ) );
		}

		public VersionSupport CheckSupport( string version, out string link, out string message )
		{
			link = String.Empty; // should specify the link to download updates from
			message = String.Empty;	// should provide version compatibility message
			try // comparing client version against loaded support configurations
			{ 
				Version clientVersion = new Version( version ); // parse version							
				foreach( VersionSupportInfo supInfo in m_VersionSupportInfoset )
				{
					// if the client version matches or exceeds the version for
					if( clientVersion >= supInfo.Version ) // the support level
					{
						link = supInfo.Link; // set appropriate download link
						message = supInfo.Message; // set appropriate message		
						return supInfo.Support; // and return support level
					}
				}
			}
			catch { /* ignore string parsing errors thrown by Version */ }
			return VersionSupport.Supported; // backward compatibility
		}

		public bool MarkerExists( string folder )
		{
			try // to check if marker exists in folder
			{
				string markerFile = GetMarkerPath( folder );
				return File.Exists( markerFile );
			}
			catch( Exception e ) 
			{
				Log.Write( "Could not check version marker",
				TraceLevel.Error, "AppVersion.MarkerExists", e ); 				
			}
			return false;
		}

		public bool WriteMarker( string folder )
		{
			try // to write a marker to folder
			{
				FileUtils.CreateFolder( folder );
				string markerFile = GetMarkerPath( folder );
				return FileUtils.TouchMarkerFile( markerFile );
			}
			catch( Exception e )
			{
				Log.Write( "Could not write version marker",
				TraceLevel.Error, "AppVersion.WriteMarker", e ); 
			}
			return false;
		}

		private string GetMarkerPath( string folder )
		{
			string file = String.Format( VERSION_MARKER_FORMAT, m_VersionString );
			return FileUtils.AppendSeparator( folder ) + file;
		}

		public override string ToString()
		{
			// use cached version
			return m_VersionString;
		}

		private struct VersionSupportInfo
		{
			public string Link;
			public string Message;	
			public Version Version;
			public VersionSupport Support;
		}

		#region NUnit Automated Test Cases

		[TestFixture] public class AppVersionTest
		{	
			[SetUp] public void SetUp()
			{
				// reset config in case already populated
				Config.UnloadConfig(); // by previous test
			}
			[TearDown] public void TearDown()
			{
				// reset config so that later tests start
				Config.UnloadConfig(); // with clean data
			}

			[Test] public void CreateAppVersionTest()
			{
				AppVersion appVersion = new AppVersion(); // set from Application.ProductVersion
                Assert.AreEqual( Application.ProductVersion, appVersion.ToString(), "Incorrect version from default constructor.");

				appVersion = new AppVersion( "y.x.q.r" ); // specify an invalid product version
				Assert.AreEqual( "0.0", appVersion.ToString(), "Incorrect version from invalid string." );

				appVersion = new AppVersion( "" ); // specify an empty product version
				Assert.AreEqual( "0.0", appVersion.ToString(), "Incorrect version from empty string." );

				appVersion = new AppVersion( null ); // specify a null product version
				Assert.AreEqual( "0.0", appVersion.ToString(), "Incorrect version from null string." );

				appVersion = new AppVersion( "2.1.5" ); // specify a valid product version
				Assert.AreEqual( "2.1.5", appVersion.ToString(), "Incorrect version from valid string." );

				appVersion = new AppVersion( "3.1" ); // specify a shorter product version
				Assert.AreEqual( "3.1", appVersion.ToString(), "Incorrect version from valid string." );
			}

			[Test] public void VersionMarkerTest()
			{
				AppVersion appVersion = new AppVersion();
				string tempFolder = FileUtils.CreateTempFolder();

				Assert.IsFalse( appVersion.MarkerExists( tempFolder ), "Unexpected marker found." );
				Assert.IsFalse( appVersion.WriteMarker( "C<|Bad Path" ), "Wrote marker at invalid path." );

				Directory.Delete( tempFolder, true ); // remove folder so WriteMarker can recreate
				
				Assert.IsTrue( appVersion.WriteMarker( tempFolder ), "Marker write failed at valid path." );
				Assert.IsTrue( appVersion.MarkerExists( tempFolder ), "Marker not found after write." );

				File.SetAttributes( appVersion.GetMarkerPath( tempFolder ), 0 );
				Directory.Delete( tempFolder, true ); // cleanup test folder
			}

			[Test] public void LoadAndCheckSupportTest()
			{
				string link, message; // output strings for future method calls
				AppVersion appVersion = new AppVersion(); // create app version without .config file

				int infosetLength = appVersion.m_VersionSupportInfoset.Length; // nothing loaded without config file
				Assert.AreEqual( 0, infosetLength, "Incorrect version support infoset loaded without config file" );
				
				VersionSupport vsup = appVersion.CheckSupport( "1.0", out link, out message );
				Assert.AreEqual( VersionSupport.Supported, vsup, "Should default to Supported without config file" );
				Assert.AreEqual( String.Empty, message, "Should default to empty Message without config file" );
				Assert.AreEqual( String.Empty, link, "Should default to empty Link without config file" );				
				
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly ); // load .config for Buzm assembly

				// update version numbers to run tests against
				Config.SetValue( CONFIG_BASE_PATH + "supported/version", "2.0.234" );
				Config.SetValue( CONFIG_BASE_PATH + "deprecated/version", "1.0.1" );
				Config.SetValue( CONFIG_BASE_PATH + "unsupported/version", "0.5.0" );

				appVersion = new AppVersion(); // recreate app version with new .config data

				vsup = appVersion.CheckSupport( "3.0", out link, out message );
				Assert.AreEqual( VersionSupport.Supported, vsup, "Expected version 3.0 to be Supported" );
				Assert.AreEqual( String.Empty, message, "Should return empty Message when Supported" );
				Assert.AreEqual( String.Empty, link, "Should return empty Link when Supported" );

				vsup = appVersion.CheckSupport( "2.0.233", out link, out message ); 
				Assert.AreEqual( VersionSupport.Deprecated, vsup, "Expected version 2.0.233 to be Deprecated" );
				Assert.IsTrue( message != String.Empty, "Should return some Message when Deprecated" );
				Assert.IsTrue( link != String.Empty, "Should return some Link when Deprecated" );

				vsup = appVersion.CheckSupport( "0.5.0.0", out link, out message );
				Assert.AreEqual( VersionSupport.Unsupported, vsup, "Expected version 0.5.0.0 to be Unsupported" );
				Assert.IsTrue( message != String.Empty, "Should return some Message when Unsupported" );
				Assert.IsTrue( link != String.Empty, "Should return some Link when Unsupported" );

				
				vsup = appVersion.CheckSupport( "0.5", out link, out message ); // 0.5 is < 0.5.0 since empty parts are < 0
				Assert.AreEqual( VersionSupport.Supported, vsup, "Expected version 0.5 to return default of Supported" );
				Assert.AreEqual( String.Empty, message, "Should return empty Message when Supported" );
				Assert.AreEqual( String.Empty, link, "Should return empty Link when Supported" );

				// if Unknown configuration already exists then set an invalid version for it
				string unknownVersion = Config.GetValue( CONFIG_BASE_PATH + "unknown/version" );				
				if( unknownVersion != String.Empty ) Config.SetValue( CONFIG_BASE_PATH + "unknown/version", "blah" );
				else // add invalid support version for Unknown configuration. LoadSupportInfo should skip this entry and load the rest
				{					
					string invalidConfig = "<unknown><link></link><message>Unknown Version</message><version>blah</version></unknown>";
					Config.AddValue( CONFIG_BASE_PATH.Trim( new char[]{'/'} ), invalidConfig );
				}
				
				appVersion = new AppVersion(); // recreate app version with new .config including invalid Unknown version
				vsup = appVersion.CheckSupport( "3.5", out link, out message ); // should fall to Supported since Unknown was skipped
				Assert.AreEqual( VersionSupport.Supported, vsup, "Expected version 3.5 to be Supported with invalid Unknown version" );
				Assert.AreEqual( String.Empty, message, "Should return empty Message when Supported" );
				Assert.AreEqual( String.Empty, link, "Should return empty Link when Supported" );

				// reset Unknown version to valid value. This would typically happen when a client is running a newer
				Config.SetValue( CONFIG_BASE_PATH + "unknown/version", "3.1.5.1" ); // version number than the server
				appVersion = new AppVersion(); // recreate app version with new .config including valid Unknown version

				vsup = appVersion.CheckSupport( "3.5", out link, out message ); // should match valid Unknown version
				Assert.AreEqual( VersionSupport.Unknown, vsup, "Expected version 3.5 to be Unknown with valid config" );
				Assert.IsTrue( message != String.Empty, "Should return some Message when Unknown" );
				Assert.AreEqual( String.Empty, link, "Should return empty Link when Unknown" );

				vsup = appVersion.CheckSupport( "blah", out link, out message ); // try matching invalid input version
				Assert.AreEqual( VersionSupport.Supported, vsup, "Invalid input version should return default of Supported" );
				Assert.AreEqual( String.Empty, message, "Should return empty Message when Supported" );
				Assert.AreEqual( String.Empty, link, "Should return empty Link when Supported" );

				vsup = appVersion.CheckSupport( "", out link, out message ); // try matching empty input version
				Assert.AreEqual( VersionSupport.Supported, vsup, "Empty input version should return default of Supported" );
				Assert.AreEqual( String.Empty, message, "Should return empty Message when Supported" );
				Assert.AreEqual( String.Empty, link, "Should return empty Link when Supported" );

				vsup = appVersion.CheckSupport( null, out link, out message ); // try matching null input version
				Assert.AreEqual( VersionSupport.Supported, vsup, "Null input version should return default of Supported" );
				Assert.AreEqual( String.Empty, message, "Should return empty Message when Supported" );
				Assert.AreEqual( String.Empty, link, "Should return empty Link when Supported" );

				// unload configuration or other nunit tests
				Config.UnloadConfig(); // will see it as well
			}
		}

		#endregion
	}

	public enum VersionSupport : int
	{
		Unknown,
		Supported,
		Deprecated,
		Unsupported
	}
}
