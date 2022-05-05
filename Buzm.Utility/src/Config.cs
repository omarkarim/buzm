using System;
using System.IO;
using System.Xml;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;
using System.Configuration;
using System.Windows.Forms;
using NUnit.Framework;

namespace Buzm.Utility
{
	/// <summary> Provides a global configuration store for 
	/// the application. By default the .config file in the 
	/// application's startup folder is loaded. Note: This class
	/// is in transition to an ApplicationSettings model.</summary>
	public class Config : ApplicationSettingsBase
	{
		private static object m_SyncRoot;
		private static XmlNode m_RootNode;		

		private static XmlDocument m_ConfigXml;
		private static string m_AssemblyCodeBase;

		private static string m_ConfigFile;
		private static readonly Config m_Settings;
	
		static Config()
		{
			m_SyncRoot = new object();	
			m_AssemblyCodeBase = Application.ExecutablePath;

			LoadConfig( m_AssemblyCodeBase + ".config" );
			m_Settings = (Config)Synchronized( new Config() );
		}

		public static void LoadConfig( string fileName )
		{
			try // to load xml config file
			{ 
				lock( m_SyncRoot )
				{
					m_ConfigFile = fileName;
					m_ConfigXml  = new XmlDocument();
					m_ConfigXml.Load( m_ConfigFile ); 
					m_RootNode = m_ConfigXml.DocumentElement;						
				}
			}
			catch( Exception e )
			{ 
				Log.Write(	"Failed to load config file: " + fileName,
				TraceLevel.Error, "Config.LoadConfig", e );
				
				// Show an error message since many features depend on config
				MessageBox.Show( "The required configuration file \"" + fileName 
								+ "\" could not be loaded. Please make sure it " 
								+ "exists and is correctly formatted.", "Buzm", 
								MessageBoxButtons.OK, MessageBoxIcon.Exclamation );
				
				// Re-throw the current exception
				throw; // The app should exit at main
			}
		}

		public static void UnloadConfig( )
		{
			try // to unload configuration
			{ 
				lock( m_SyncRoot )
				{
					m_ConfigFile = "";
					m_ConfigXml  = new XmlDocument();
					m_ConfigXml.LoadXml( "<configuration></configuration>" ); 
					m_RootNode = m_ConfigXml.DocumentElement; // empty root				
				}
			}
			catch( Exception e )
			{ 
				Log.Write(	"Failed to unload configuration",
				TraceLevel.Error, "Config.UnloadConfig", e );
			}
		}

		/// <summary>Loads config file from the current execution
		/// folder of the given assembly for NUnit cases </summary>
		public static void LoadAssemblyConfig( Assembly assembly )
		{
			Uri code = new Uri( assembly.CodeBase );
			m_AssemblyCodeBase = code.LocalPath; // remove uri scheme
			string folder = Path.GetDirectoryName( m_AssemblyCodeBase );
			string defaultConfig = folder + @"\" + "Buzm.exe.config";

			// if default buzm config file exists in the assembly folder
			if( File.Exists( defaultConfig ) ) LoadConfig( defaultConfig );
			else LoadConfig( m_AssemblyCodeBase + ".config" );
		}

		public static string GetExecutableFolder()
		{
			string executablePath = Application.ExecutablePath;
			return Path.GetDirectoryName( executablePath );
		}

		public static int GetIntValue( string path )
		{
			return GetIntValue( m_RootNode, path, 0 );
		}

		public static int GetIntValue( string path, int defaultValue )
		{
			return GetIntValue( m_RootNode, path, defaultValue );
		}

		public static int GetIntValue( XmlNode node, string path )
		{
			return GetIntValue( node, path, 0 );
		}

		public static int GetIntValue( XmlNode node, string path, int defaultValue )
		{
			string val = GetValue( node, path );
			try { return Convert.ToInt32( val ); }
			catch
			{
				Log.Write(	"Could not parse integer value: " + path + "=" + val, 
				TraceLevel.Warning, "Config.GetIntValue" );
				return defaultValue; 
			}
		}

		public static bool GetBoolValue( string path, bool defaultValue )
		{
			return GetBoolValue( m_RootNode, path, defaultValue );
		}

		public static bool GetBoolValue( XmlNode node, string path, bool defaultValue )
		{
			string val = GetValue( node, path );
			try { return Convert.ToBoolean( val ); }
			catch
			{
				Log.Write(	"Could not parse boolean value: " + path + "=" + val,  
				TraceLevel.Warning, "Config.GetBoolValue" );
				return defaultValue; 
			}
		}

		/// <summary>Returns absolute path for folder
		/// name at the specified config value. Folder
		/// value will include trailing slash </summary>
		public static string GetFolderValue( string path )
		{
			string folder = GetFileValue( path );
			if( (folder != "") && !folder.EndsWith( @"/" ) ) folder += @"/";
			return folder;
		}


		/// <summary>Returns absolute path for file
		/// name at the specified config value </summary>
		public static string GetFileValue( string path )
		{
			try // returning absolute path for folder
			{
				string exePath;
				string configFolder = GetValue( path );
				
				// if path is already absolute return it
				if( Path.IsPathRooted( configFolder ) ) return configFolder;
				else
				{	// build absolute path based on the startup folder
					exePath = Path.GetDirectoryName( m_AssemblyCodeBase );
					return Path.Combine( exePath, configFolder );	
				}
			}
			catch { return ""; }
		}

		public static string GetValue( string path )
		{
			return GetValue( m_RootNode, path );
		}

		public static string GetValue( XmlNode node, string path )
		{
			try // reading value for existing path
			{
				lock( m_SyncRoot )
				{
					XmlNode child = node.SelectSingleNode( path );
					if( child != null ) return child.InnerText;
					else return "";
				}
			}
			catch( Exception e )
			{
				Log.Write(	"Failed to get config: " + path,
				TraceLevel.Warning, "Config.GetValue", e );
				return "";
			}
		}

		public static string GetOuterXml( string path )
		{
			return GetOuterXml( m_RootNode, path );
		}

		public static string GetOuterXml( XmlNode node, string path )
		{
			try // reading xml for existing path
			{
				lock( m_SyncRoot )
				{
					XmlNode child = node.SelectSingleNode( path );
					if( child != null ) return child.OuterXml;
					else return "";
				}
			}
			catch( Exception e )
			{
				Log.Write(	"Failed to get config: " + path,
				TraceLevel.Warning, "Config.GetOuterXml", e );
				return "";
			}
		}

		public static XmlNodeList GetValues( string path )
		{
			return GetValues( m_RootNode, path );
		}

		public static XmlNodeList GetValues( XmlNode node, string path )
		{
			try // getting values for existing path
			{
				lock( m_SyncRoot )
				{
					// Return node's children
					return node.SelectNodes( path );
				}
			}
			catch( Exception e )
			{
				Log.Write(	"Failed to get config: " + path,
				TraceLevel.Warning, "Config.GetValues", e );
				return null;
			}
		}

		public static void SetValue( string path, string val )
		{
			SetValue( m_RootNode, path, val );
		}	

		/// <summary> This method can be used to set the inner text for a  
		/// single node by using xpath. For example the path to update 
		/// the name of a hive is "hives/hive[@guid='guid']/name". If the new
		/// value is an xml string, AddValue should be used instead.</summary>
		public static void SetValue( XmlNode node, string path, string val )
		{
			try // saving new value for existing path
			{				
				lock( m_SyncRoot )
				{
					XmlNode child = node.SelectSingleNode( path );
					if( child != null )
					{
						child.InnerText = val;
						m_ConfigXml.Save( m_ConfigFile );
					}
				}				
			}
			catch( Exception e )
			{
				Log.Write(	"Failed to save: " + path + "=" + val,
				TraceLevel.Warning, "Config.SetValue", e );
			}
		}

		public static void AddValue( string path, string val )
		{
			AddValue( m_RootNode, path, val );
		}
		
		public static void AddValue( XmlNode node, string path, string val )
		{
			try // adding new child to existing path
			{				
				lock( m_SyncRoot )
				{
					XmlDocumentFragment child;
					XmlNode parent = node.SelectSingleNode( path );

					if( parent != null )
					{
						child = m_ConfigXml.CreateDocumentFragment();
						child.InnerXml = val;
						parent.AppendChild( child );
						m_ConfigXml.Save( m_ConfigFile );
					}
				}				
			}
			catch( Exception e )
			{
				Log.Write(	"Failed to add value: " + path + "=" + val,
				TraceLevel.Warning, "Config.AddValue", e );
			}
		}

		public override void Save()
		{
			try // to save user settings
			{
				base.Save(); // fires SettingsSaving
				
				Log.Write( "Saved user settings to disk",
				TraceLevel.Verbose, "Config.Save" );
			}
			catch( Exception e )
			{
				Log.Write( "Failed to save user settings",
				TraceLevel.Error, "Config.Save", e );
			}
		}

		public static Config Settings
		{
			get { return m_Settings; }
		}

		#region User Scoped Config Settings

		[UserScopedSetting]
		public Rectangle? WindowBounds
		{
			get { return this["WindowBounds"] as Rectangle?; }
			set { this["WindowBounds"] = value; }
		}

		[UserScopedSetting]
		public FormWindowState? WindowState
		{
			get { return this["WindowState"] as FormWindowState?; }
			set { this["WindowState"] = value; }
		}

		[UserScopedSetting, DefaultSettingValue("0")]
		public int WindowSplitter
		{
			get { return (int)this["WindowSplitter"]; }
			set { this["WindowSplitter"] = value; }
		}

		[UserScopedSetting, DefaultSettingValue("")]
		public string AutoLogin
		{
			get { return (string)this["AutoLogin"]; }
			set { this["AutoLogin"] = value; }
		}

		#endregion

		#region NUnit Automated Test Cases

		[TestFixture] public class ConfigTest
		{
			[SetUp] public void SetUp() 
			{ 
				// Load local config file for the Buzm.Utility assembly
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly );
			}

			[TearDown] public void TearDown() 
			{ 
				// unload configuration or other nunit tests
				Config.UnloadConfig(); // will see it as well
			}
			
			[Test] public void GetValueTest()
			{
				string val = Config.GetValue( "network/defaultPort" );
				Assert.IsNotEmpty( val, "Got no value from config file" );
			}

			[Test] public void SynchronizedTest()
			{
				bool sync = Config.Settings.IsSynchronized;
				Assert.IsTrue( sync, "Expected synchronized settings" );
			}

			[Test] public void SaveUserSettingTest()
			{				
				Rectangle? savedRect = Config.Settings.WindowBounds;
				Rectangle? newRect = new Rectangle( 12, 19, 19, 73 );

				Config.Settings.WindowBounds = newRect;
				Config.Settings.Save(); // for next test run

				// assert will fail when run for the first time
				Assert.AreEqual( newRect, savedRect, "Should pass if test is run again" );
			}
		}

		#endregion

	}
}
