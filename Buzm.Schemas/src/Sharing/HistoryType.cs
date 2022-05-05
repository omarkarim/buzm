using System;
using System.Collections;
using System.Xml.Serialization;
using NUnit.Framework;

namespace Buzm.Schemas.Sharing
{
	[XmlRootAttribute( "history", Namespace = "http://www.microsoft.com/schemas/rss/sse" )]
	public class HistoryType : UpdateType
	{
		private ArrayList m_Updates;
		private const int MAX_UPDATES = 25;
		
		public HistoryType() { m_Updates = new ArrayList(); }

		[XmlElement( "update", typeof(UpdateType) )] public ArrayList Updates
		{ get { return m_Updates; } set { m_Updates = value; } }

		public void AddUpdate( DateTime when, string by )
		{	
			// copy history info to new update
			UpdateType update = new UpdateType();
			update.When = this.When;
			update.By = this.By;

			// insert update at start
			m_Updates.Insert( 0, update );

			// truncate update records
			if( m_Updates.Count > MAX_UPDATES )
			{
				m_Updates.RemoveRange( MAX_UPDATES, 
				m_Updates.Count - MAX_UPDATES );
			}

			// modify history
			this.When = when;
			this.By = by;
		}

		#region NUnit Automated Test Cases
		#if DEBUG

		[TestFixture] public class HistoryTypeTest
		{
			[SetUp] public void SetUp() { }
			[TearDown] public void TearDown() { }

			[Test] public void AddUpdateTest()
			{
				string user = "okarim";
				DateTime now = DateTime.Now;
				
				HistoryType history = new HistoryType();
				history.AddUpdate( now, user );

				Assert.AreEqual( now, history.When, "Got incorrect history When after update" );
				Assert.AreEqual( user, history.By, "Got incorrect history By after update" );

				UpdateType firstUpdate = (UpdateType)history.Updates[0];
				Assert.AreEqual( DateTime.MinValue, firstUpdate.When, "Expected min When from update" );
				Assert.IsNull( firstUpdate.By, "Expected null By from update" );

				history = new HistoryType(); // reset history								
				int twiceMaxUpdates = HistoryType.MAX_UPDATES * 2;
				
				for( int i=1; i <= twiceMaxUpdates; i++ )
				{
					// test truncation by adding too many updates
					history.AddUpdate( now.AddDays( i ), user + i );
				}

				Assert.AreEqual( now.AddDays( twiceMaxUpdates ), history.When, "Got incorrect history When after max updates" );
				Assert.AreEqual( user + twiceMaxUpdates, history.By, "Got incorrect history By after max updates" );
				Assert.AreEqual( HistoryType.MAX_UPDATES, history.Updates.Count, "Expected max update count after truncate" );

				for( int ii = 1; ii <= 5; ii++ )
				{
					// also add some out of band updates
					int index = twiceMaxUpdates + ii;
					UpdateType update = new UpdateType();

					update.When = now.AddDays( index );
					update.By = user + index;					
					history.Updates.Add( update );
				}
				
				// truncate out of band updates
				history.AddUpdate( now, user );

				Assert.AreEqual( now, history.When, "Got incorrect history When after out of band updates" );
				Assert.AreEqual( user, history.By, "Got incorrect history By after out of band updates" );
				Assert.AreEqual( HistoryType.MAX_UPDATES, history.Updates.Count, "Expected max update count after out of band updates" );

				firstUpdate = (UpdateType)history.Updates[0];
				Assert.AreEqual( now.AddDays( twiceMaxUpdates ), firstUpdate.When, "Expected max*2 When from first update" );
				Assert.AreEqual( user + twiceMaxUpdates, firstUpdate.By, "Expected max*2 By from first update" );

				UpdateType lastUpdate = (UpdateType)history.Updates[history.Updates.Count - 1];
				Assert.AreEqual( now.AddDays( HistoryType.MAX_UPDATES + 1 ), lastUpdate.When, "Expected max+1 When from last update" );
				Assert.AreEqual( user + (HistoryType.MAX_UPDATES + 1), lastUpdate.By, "Expected max+1 By from last update" );		
			}
		}

		#endif
		#endregion
	}
}
