using System;
using System.Reflection;
using System.Threading;
using Buzm.Network.Packets;
using Buzm.Network.Sockets;
using NUnit.Framework;
using Buzm.Utility;

namespace Buzm
{
	/// <summary>SingleInstance uses a mutex synchronization 
	/// object to ensure that only one copy of a process is active 
	/// for a specific user at a particular time. It also provides a 
	/// method to transfer arguments to the active instance</summary>
	public class SingleInstance : IDisposable
	{
		private int m_ServerPort;
		private Mutex m_ProcessSync;
		private bool m_OwnMutex = false;
		private bool m_Disposed = false;
		private bool m_Activated = false;
		private const int ACTIVATE_WAIT = 5000;

		public SingleInstance() : this( "" ) { }
		public SingleInstance( string identifier )
		{
			// get configured server port for application instance
			m_ServerPort = Config.GetIntValue( "network/defaultPort" );

			// Initialize a named mutex and attempt to get ownership immediately.
			// Use the Buzm server port and additional identifier to lower our 
			// chances of another process creating a mutex with the same name.
			m_ProcessSync = new Mutex( true, GenerateUniqeName() + identifier
			+ m_ServerPort.ToString(), out m_OwnMutex ); // true if got mutex
		}

		public bool ActivatePriorInstance( string[] args )
		{
			if( m_ServerPort != 0 ) // if valid port has been specified
			{
				try // to activate prior Buzm instance and pass args to it
				{
					PeerEndPoint pep = new PeerEndPoint( "localhost", m_ServerPort );
					Servent srv = new Servent( pep ); // connect to local Buzm instance
					srv.DataReceived += new DataReceivedEventHandler( Servent_DataReceived );

					lock( this ) // synchronize response from prior instance
					{
						srv.BeginReceive(); // initialize socket state
						ArgsPacket argsPkt = new ArgsPacket( args );
						srv.Send( argsPkt, false ); // send args
					
						// wait for response from instance
						Monitor.Wait( this, ACTIVATE_WAIT );
						//srv.Close(); // race condition
						return m_Activated;
					}
				}
				catch { return false; /* activation failed */ }
			}
			else return false; // no connection port was specified
		}

		private void Servent_DataReceived( object data, Servent srv )
		{
			try // to process response from prior instance
			{
				lock( this ) // synchronize instance response
				{
					if( data is ArgsPacket ) // if valid packet
					{
						ArgsPacket pkt = (ArgsPacket)data;
						m_Activated = pkt.ArgsReceived;
					}
					else m_Activated = false;
					Monitor.Pulse( this );
				}
			}
			catch { /* response processing failed */ }
		}

		private static string GenerateUniqeName()
		{
			return Environment.UserName // logged in user
			+ Assembly.GetExecutingAssembly().GetName().Name;
		}

		public bool IsSingleInstance
		{ // If we don't own the mutex than
			// we are not the first instance.
			get { return m_OwnMutex; }
		}

		#region Dispose Pattern Methods

		~SingleInstance()
		{
			//Release mutex (if necessary) 
			//This should have been accomplished using Dispose() 
			Dispose( false );
		}

		public void Dispose()
		{
			// release mutex (if necessary) and notify 
			// the garbage collector to ignore the destructor
			Dispose( true );
			GC.SuppressFinalize( this );
		}

		/// <summary>Cleans up resources</summary>
		/// <param name="disposing">True when called 
		/// by user code, false if called by GC</param>
		protected virtual void Dispose( bool disposing )
		{
			if( !m_Disposed )
			{
				if( disposing )
				{
					m_ProcessSync.Close();
					m_OwnMutex = false;
				}
			}
			m_Disposed = true;
		}

		#endregion		

		#region NUnit Automated Test Cases

		[TestFixture] public class SingleInstanceTest
		{
			[SetUp] public void SetUp() 
			{ 
				// Load local config file for this assembly
				Assembly assembly = Assembly.GetAssembly( this.GetType() );
				Config.LoadAssemblyConfig( assembly );
			}

			[TearDown] public void TearDown()
			{ 
				// unload configuration or other nunit tests
				Config.UnloadConfig(); // will see it as well
			}

			[Test] public void ShouldNotAllowMultipleTest()
			{
				// first time we try the mutex it should be true
				SingleInstance siOne = new SingleInstance( "123" );
				Assertion.Assert( "Tried to get new mutex", siOne.IsSingleInstance );

				// next time we try the mutex it should be false
				SingleInstance siTwo = new SingleInstance( "123" );
				Assertion.Assert( "Tried to get existing mutex", !siTwo.IsSingleInstance );
				
				// release mutex resources
				siOne.Dispose(); siTwo.Dispose();

				// try to get available mutex once again
				SingleInstance siThree  = new SingleInstance( "123" );
				Assertion.Assert( "Tried to get disposed mutex", siThree.IsSingleInstance );

				siThree = null;
				GC.Collect(); // auto release
				GC.WaitForPendingFinalizers(); 
				
				// try to get available mutex once again
				SingleInstance siFour  = new SingleInstance( "123" );
				Assertion.Assert( "Tried to get collected mutex", siFour.IsSingleInstance );
			}

			[Test] public void ActivatePriorInstanceTest()
			{
				string[] args = new string[]{ "-i", @"C:\Invite.buz" };
				SingleInstance instance = new SingleInstance( "456" );
				
				// try to pass args to a non-existent process
				bool activated = instance.ActivatePriorInstance( args );
				Assertion.Assert( "Tried passing args to non-existent process", !activated );

				// create new peer manager on random port
				PeerManager mgr = new PeerManager( 9335, null );
				activated = instance.ActivatePriorInstance( args );
				Assertion.Assert( "Tried passing args to process on wrong port", !activated );
				
				mgr.Close(); // cleanup resources for existing peer manager
				mgr = new PeerManager( null ); // create another one on the default port
				activated = instance.ActivatePriorInstance( args ); // call should succeed 
				Assertion.Assert( "Tried passing args to process on correct port", activated );

				ArgsPacket pkt = (ArgsPacket)mgr.GetNextPacket();
				string[] receivedArgs = pkt.Arguments; // extract args on receiving end
				Assertion.AssertEquals( "Got incorrect arg count", 2, receivedArgs.Length );
				Assertion.AssertEquals( "Got incorrect argument value", "-i", receivedArgs[0] );
				Assertion.AssertEquals( "Got incorrect argument value", @"C:\Invite.buz", receivedArgs[1] );

				mgr.Close(); // cleanup resources for peer manager
				instance.Dispose(); // release the instance mutex
			}
		}

		#endregion
	}
}
