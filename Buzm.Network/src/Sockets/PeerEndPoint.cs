using System;

namespace Buzm.Network.Sockets
{
	[Serializable] public class PeerEndPoint
	{
		private int	   m_Port;		
		private string m_Host;
		private string m_Guid;
		private string m_Version;

		// These vars not sent over the network
		[NonSerialized] private int m_RetryCount;
		[NonSerialized] private int m_MaxRetries;
		[NonSerialized] private int m_RetryWaitSecs;
		[NonSerialized] private Random m_RandFactory;
		[NonSerialized] private DateTime m_LastRetryTime;
		[NonSerialized] private DateTime m_NextRetryTime;		
		[NonSerialized] private const int RETRY_WAIT = 0;
		[NonSerialized] private const int MAX_RETRIES = 21; 
		// MAX_RETRY value of 21 equates to ~24-48 day range	

		public PeerEndPoint( string host, int port )
		{ 	
			m_Host = host;
			m_Port = port;
			m_Guid = String.Empty;
			m_Version = String.Empty;
			
			// retry stats
			m_RetryCount = 0;
			m_MaxRetries = MAX_RETRIES;
			m_RetryWaitSecs = RETRY_WAIT;
			
			// create a pseudo random seed based on the time and host
			int seed = unchecked((int)DateTime.Now.Ticks) - host.Length; 
			m_RandFactory = new Random( seed ); // used for retry timeouts
		}

		public PeerEndPoint( string host, int port, string guid ) : this( host, port )		
		{ 		
			m_Guid = guid;
		}

		/// <summary> Returns true if a connection to this end point should
		///  be attempted right now based on the retry configuration </summary>
		public bool ShouldConnectNow( )
		{
			DateTime nowTime = DateTime.Now;
			// if never tried connect before
			if( m_LastRetryTime.Ticks == 0 )
			{ 
				// Save time for next try
				m_LastRetryTime = nowTime;
				m_RetryCount++;
				return true; 
			}
			else
			{	// return false if max retries are exceeded
				if( m_RetryCount >= m_MaxRetries ) return false;
				else 
				{	// calculate next retry time
					if( m_NextRetryTime.Ticks == 0 )
					{						
						// randomized exponential growth rate of 2^n secs 
						int minRetrySecs = (int)Math.Pow( 2, m_RetryCount );
						int maxRetrySecs = (int)Math.Pow( 2, m_RetryCount + 1 );
						int newRetrySecs = m_RandFactory.Next( minRetrySecs, maxRetrySecs );						
						m_NextRetryTime  = m_LastRetryTime.AddSeconds( newRetrySecs );		
					}	
					
					// return true if retry time is in the past
					if( m_NextRetryTime < nowTime )
					{
						// Reset times for next attempt
						m_NextRetryTime = DateTime.MinValue;
						m_LastRetryTime = nowTime; 	
						m_RetryCount++;
						return true; 
					}
				}
			}
			return false;
		}

		/// <summary> Reset retry statistics
		/// for new sequence of attempts </summary>
		public void ResetRetryStats( )
		{
			m_RetryCount = 0;
			m_LastRetryTime = DateTime.Now;
			m_NextRetryTime = m_LastRetryTime.AddSeconds( m_RetryWaitSecs );
		}
		
		public string Host
		{
			get { return m_Host; }
			set { m_Host = value; }
		}

		public int Port
		{
			get { return m_Port; }
			set { m_Port = value; }
		}

		public string Guid
		{
			get { return m_Guid; }
			set { m_Guid = value; }
		}

		public string Version
		{
			get { return m_Version; }
			set { m_Version = value; }
		}

		public int RetryCount
		{
			get { return m_RetryCount; }
			set { m_RetryCount = value; }
		}

		/// <summary> Number of seconds to wait before
		/// initiating the normal retry sequence </summary>
		public int RetryWait
		{
			get { return m_RetryWaitSecs; }
			set { m_RetryWaitSecs = value; }
		}

		public int MaxRetries
		{
			get { return m_MaxRetries; }
			set { m_MaxRetries = value; }
		}

		public override string ToString()
		{
			return m_Host + ":" + m_Port.ToString() + " (" 
				   + m_RetryCount.ToString() + " retries)";
		}
	}
}