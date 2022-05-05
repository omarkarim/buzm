using System;
using System.Collections;
using NUnit.Framework;

namespace Buzm.Utility
{
	/// <summary> Provides a simple thread-safe FIFO cache  
	/// with fast hash lookups and auto-truncation </summary>
	public class HashQueue
	{
		private int m_MaxSize;
		private Queue m_Queue;
		private object m_SyncRoot;
		private Hashtable m_Hashtable;

		public HashQueue(): this( 0 ){}
		public HashQueue( int maxSize )
		{ 	
			m_MaxSize = maxSize;
			m_Queue = new Queue();
			m_SyncRoot = new Object();
			m_Hashtable = new Hashtable();
		}
		
		public void Enqueue( object key, object val )
		{
			lock( m_SyncRoot )
			{
				if( !m_Hashtable.Contains( key ) )
				{
					m_Queue.Enqueue( key );
					m_Hashtable.Add( key, val );
					
					// If maximum permissible size has been exceeded
					if( (m_MaxSize != 0) && (m_Queue.Count > m_MaxSize) ) 
					{
						object oldestObj = m_Queue.Dequeue();
						m_Hashtable.Remove( oldestObj );
					}
				}
			}
		}

		public bool Contains( object key )
		{
			bool contains = false;
			lock( m_SyncRoot ){ contains = m_Hashtable.Contains( key ); }
			return contains;
		}

		public int Count( )
		{
			int count = 0;
			lock( m_SyncRoot ){ count = m_Queue.Count; }
			return count;
		}

		public object SyncRoot
		{ 
			get { return m_SyncRoot; }
		}

		#region NUnit Automated Test Cases

		[TestFixture] public class HashQueueTest
		{
			private int m_MaxQueueSize;
			private HashQueue m_HashQueue;

			[SetUp] public void SetUp()
			{ 
				m_MaxQueueSize = 10;
				m_HashQueue = new HashQueue( m_MaxQueueSize );
			}
			
			[TearDown] public void TearDown(){ }

			[Test] public void CheckMaxQueueSize()
			{
				// Fill the queue half way
				for( int i=0; i < (m_MaxQueueSize/2); i++ )
				{
					m_HashQueue.Enqueue( i, null );
				}
				
				// See if count is accurate. Private vars are accessed directly since this is a nested class.
				Assertion.AssertEquals( "Incorrect inner Hashtable count.", (m_MaxQueueSize/2), m_HashQueue.m_Hashtable.Count );
				Assertion.AssertEquals( "Incorrect inner Queue count.", (m_MaxQueueSize/2), m_HashQueue.m_Queue.Count );

				// Fill the queue to double the maximum
				// First quarter are ignored as duplicates
				for( int i=0; i < (m_MaxQueueSize*2); i++ )
				{
					m_HashQueue.Enqueue( i, null );
				}

				// See if count is accurate. Should be m_MaxQueueSize.
				Assertion.AssertEquals( "Incorrect inner Hashtable count.", m_MaxQueueSize, m_HashQueue.m_Hashtable.Count );
				Assertion.AssertEquals( "Incorrect inner Queue count.", m_MaxQueueSize, m_HashQueue.m_Queue.Count );

				// See if HashQueue contains the correct items
				Assertion.Assert( "Dequeued item found.", !m_HashQueue.Contains( m_MaxQueueSize - 1 ) );
				Assertion.Assert( "Enqueued item not found.", m_HashQueue.Contains( (m_MaxQueueSize*2) - 1 ) );
			}
		}

		#endregion

	}
}