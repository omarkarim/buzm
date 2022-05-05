using System;
using NUnit.Framework;

namespace Buzm.Email
{
    /// <summary>
    /// Represents the result of an MX Record query to a DNS Server.
    /// </summary>
    public class MXRecord : IComparable
    {
        public MXRecord (string hostname, int priority)
        {
            this.m_hostname = hostname;
            this.m_priority = priority;
        }

        /// <summary>
        /// The hostname of the mail server.
        /// </summary>
        public string Hostname
        {
            get { return m_hostname; }
        }

        /// <summary>
        /// The priority in which the mail server should be used to send mail.  
        /// Lower numbers represent higher priority.
        /// </summary>
        public int Priority
        {
            get { return m_priority; }
        }

        private string m_hostname = null;
        private int m_priority = -1;

        #region IComparable Members

        /// <summary>
        /// MXRecords are sorted in ascending priority order.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>A 32-bit signed integer that indicates the relative order of the comparands.  
        /// The return value has these meanings:
        ///    Value Meaning
        ///        Less than zero This instance is less than obj.
        ///        Zero This instance is equal to obj.
        ///        Greater than zero This instance is greater than obj.    
        /// </returns>
        public int CompareTo (object obj)
        {
            MXRecord other = (MXRecord) obj;
            return this.Priority.CompareTo (other.Priority);
        }

        #endregion

        #region NUnit Automated Test Cases

        [TestFixture, Ignore("Test times out on occasion")]
        public class MXRecordTest
        {
            [SetUp]
            public void SetUp ()
            {
            }

            [TearDown]
            public void TearDown ()
            {
            }

            [Test]
            public void TestConstructor ()
            {
                MXRecord record = new MXRecord ("hostname", 1);
                Assertion.AssertEquals ("hostname", record.Hostname);
                Assertion.AssertEquals (1, record.Priority);
            }

            public void TestCompare ()
            {
                MXRecord record = new MXRecord ("hostname", 1);
				MXRecord record2 = new MXRecord ("hostname", 2);
				MXRecord record3 = new MXRecord ("hostname", 1);

				Assertion.AssertEquals (0, record.CompareTo(record3));
                Assertion.Assert(record.CompareTo(record2) < 0);
				Assertion.Assert(record2.CompareTo(record) > 0);
			}
        }

        #endregion

    }
}