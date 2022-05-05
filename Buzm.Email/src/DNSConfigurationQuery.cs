using System;
using System.Collections;
using System.Management;
using NUnit.Framework;
using System.Net;

namespace Buzm.Email
{
    /// <summary>
    /// Queries the local system for configured DNS servers.
    /// </summary>
    public class DNSConfigurationQuery
    {
        private static DNSConfigurationQuery dnsConfigurationQuery;
        private ArrayList m_dnsServers;

        /// <summary> 
        /// Gets the Singleton instance of DNSConfiguration.
        /// </summary>
        public static DNSConfigurationQuery GetInstance ()
        {
            if (dnsConfigurationQuery == null)
            {
                dnsConfigurationQuery = new DNSConfigurationQuery ();
            }
            return dnsConfigurationQuery;
        }

        /// <summary>
        /// Returns the currently configured list of DNS Servers.
        /// Call Refresh() to re-query the local system.
        /// </summary>
        /// <returns>The list of configured DNS Servers.</returns>
        public IList GetDNSServers ()
        {
            return this.m_dnsServers;
        }

        /// <summary>
        /// Convenience method to get the first configured DNS Server.
        /// </summary>
        /// <returns>The first DNS server in the list of configured DNS Servers.</returns>
        public string GetFirstDNSServer ()
        {
            return (string) this.m_dnsServers[0];
        }

        /// <summary> 
        /// Constructs a new DNSConfiguration.
        /// Queries the local system for the configured DNS Servers.
        /// </summary>
        private DNSConfigurationQuery ()
        {
            m_dnsServers = new ArrayList ();
            Refresh ();
        }

        /// <summary>
        /// Refresh the list of DNS servers.
        /// </summary>
        public void Refresh ()
        {
            ManagementObjectSearcher query = new ManagementObjectSearcher ("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IpEnabled = True");
            ManagementObjectCollection queryCollection = query.Get ();
            m_dnsServers.Clear ();
            foreach (ManagementObject mo in queryCollection)
            {
                string[] DNSsearch = (string[]) mo["DNSServerSearchOrder"];
                if (DNSsearch != null)
                {
                    foreach (string s in DNSsearch)
                    {
                        m_dnsServers.Add (s);
                    }
                }
            }
        }

        #region NUnit Automated Test Cases

        [TestFixture, Ignore("Test times out on occasion")]
        public class DNSConfigurationTest
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
            public void TestGetInstance ()
            {
                // Get the Singleton Instance
                DNSConfigurationQuery dnsConfigurationQuery = DNSConfigurationQuery.GetInstance ();
                DNSConfigurationQuery dnsConfiguration2 = DNSConfigurationQuery.GetInstance ();
                Assertion.AssertSame (dnsConfigurationQuery, dnsConfiguration2);
            }

            [Test]
            public void TestGetConfiguredDNSServers ()
            {
                // Get the Singleton Instance
                DNSConfigurationQuery dnsConfigurationQuery = DNSConfigurationQuery.GetInstance ();
                ICollection servers = dnsConfigurationQuery.GetDNSServers ();
                Assertion.AssertNotNull (servers);
                Assertion.Assert (servers.Count > 0);

                IEnumerator serverEnum = servers.GetEnumerator ();
                // Test whether the list of strings is in fact a list of IP Addresses
                while (serverEnum.MoveNext ())
                {
                    string nextDNS = (string) serverEnum.Current;
                    // Resolve the IP
                    IPHostEntry hostEntry = Dns.Resolve (nextDNS);
                    // IP should match string representation
                    Assertion.AssertEquals (1, hostEntry.AddressList.Length);
                    Assertion.AssertEquals (hostEntry.AddressList.GetValue (0).ToString (), nextDNS);
                }
            }

            [Test]
            public void TestRefresh ()
            {
                // Get the Singleton Instance
                DNSConfigurationQuery dnsConfigurationQuery = DNSConfigurationQuery.GetInstance ();
                ICollection servers = dnsConfigurationQuery.GetDNSServers ();
                dnsConfigurationQuery.Refresh ();
                ICollection servers2 = dnsConfigurationQuery.GetDNSServers ();
                Assertion.AssertEquals (servers, servers2);
            }

            [Test]
            public void TestGetFirstDNSServer ()
            {
                // Get the Singleton Instance
                DNSConfigurationQuery dnsConfigurationQuery = DNSConfigurationQuery.GetInstance ();
                string server = dnsConfigurationQuery.GetFirstDNSServer ();
                Assertion.AssertNotNull (server);
            }

            #endregion

        }
    }
}