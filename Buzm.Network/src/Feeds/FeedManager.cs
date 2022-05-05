using System;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.ComponentModel;
using Buzm.Utility;
using Buzm.Network;
using Buzm.Network.Packets;

namespace Buzm.Network.Feeds
{
	public class FeedManager : INetworkManager
	{
		bool m_NotifyUser;
		private Hashtable m_Feeds;
		private Queue m_ContentQueue;
		private Thread m_ManagerThread;

		private ISynchronizeInvoke m_SyncObject;
		private const int FEED_UPDATE_TIMEOUT = 60000;

		public FeedManager( ISynchronizeInvoke sync )
		{ 	
			m_SyncObject = sync;
			m_NotifyUser = false;
			m_Feeds = new Hashtable();
			m_ContentQueue = new Queue();

			// All feed creation and processing done by the manager thread
			m_ManagerThread = new Thread( new ThreadStart( ManageFeeds ) );
			m_ManagerThread.Start(); // start thread to update feed content
		}
		
		private void ManageFeeds( )
		{
			// loop variables
			string feedContent;
			FeedModel[] feedModels;
			
			Thread.CurrentThread.Name = "FeedManager";
			while( true ) // infinite feed update loop
			{
				try // updating each of the configured feed models
				{  
					// copy all feeds to a static array to avoid extended lock
					lock( m_Feeds.SyncRoot ) // since feed updates can take time
					{
						feedModels = new FeedModel[m_Feeds.Values.Count];
						m_Feeds.Values.CopyTo( feedModels, 0 );
					}

					// update static collection of feeds
					foreach( FeedModel feed in feedModels )
					{ 
						if( feed.CheckForUpdates() )
						{
							feedContent = feed.ToXml(); // get Buzm xml format feed
							if( ( feedContent != null ) && ( feedContent != String.Empty ) )
							{
								SetNextPacket( new FeedPacket( feedContent, feed.HiveGuid, feed.Guid ) );
								Log.Write( "Feed content was updated: " + feedContent, 
								TraceLevel.Verbose, "FeedManager.ManageFeeds" );
							}
						}
					}

					// Wait for feed sources to change
					Thread.Sleep( FEED_UPDATE_TIMEOUT );

				}
				catch( ThreadAbortException )
				{ 
					// Occurs normally when the thread is aborted during shutdown
					Log.Write( "Thread " + Thread.CurrentThread.Name + " was aborted",
					TraceLevel.Verbose, "FeedManager.ManageFeeds" );
				}
				catch( Exception e )
				{ 
					// Safety net for all unexpected errors that may occur
					Log.Write( "Could not read or process configured feed",
					TraceLevel.Warning, "FeedManager.ManageFeeds", e );
				}
			}
		}

		public Packet GetNextPacket()
		{
			Packet pkt = null;
			lock( m_ContentQueue.SyncRoot )
			{ 
				if( m_ContentQueue.Count > 0 )
				{	pkt = (Packet)m_ContentQueue.Dequeue(); }
			}
			return pkt;
		}

		public void SetNextPacket( Packet pkt )
		{
			lock( m_ContentQueue.SyncRoot )
			{
				m_ContentQueue.Enqueue( pkt );
			}
		}

		public void RegisterFeed( FeedModel feed )
		{
			lock( m_Feeds.SyncRoot )
			{
				if( !m_Feeds.Contains( feed.Guid ) )
				{
					m_Feeds.Add( feed.Guid, feed ); 
				}
			}
			Log.Write( "Feed added: " + feed.Guid + ":" + feed.Url, 
			TraceLevel.Verbose, "FeedManager.RegisterFeed" );
		}

		public void UnregisterFeed( FeedModel feed )
		{
			lock( m_Feeds.SyncRoot )
			{
				if( m_Feeds.Contains( feed.Guid ) )
				{
					m_Feeds.Remove( feed.Guid ); 
				}
			}
			Log.Write( "Feed removed: " + feed.Guid + ":" + feed.Url, 
			TraceLevel.Verbose, "FeedManager.UnregisterFeed" );
		}

		public void HiveManager_FeedAdded( object sender, ModelEventArgs e )
		{
			if( e.Model is FeedModel ) // ensure model is correct type
			{
				FeedModel feed = (FeedModel)e.Model; 
				RegisterFeed( feed ); // activate feed
			}
		}

		public void HiveManager_FeedRemoved( object sender, ModelEventArgs e )
		{
			if( e.Model is FeedModel ) // ensure model is correct type
			{
				FeedModel feed = (FeedModel)e.Model; 
				UnregisterFeed( feed ); // disable feed
			}
		}

		public void Close()
		{
			if( m_ManagerThread != null )
			{
				m_ManagerThread.Abort();
				m_ManagerThread.Join();
			}
		
		}

		/// <summary> INetworkManager property that determines if the user
		/// should be alerted to packets received through this network </summary>
		public bool NotifyUser
		{
			get { return m_NotifyUser; }
			set { m_NotifyUser = value; }
		}
	}
}