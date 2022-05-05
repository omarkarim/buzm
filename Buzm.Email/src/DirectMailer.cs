using System;
using System.Net;
using System.Net.Sockets;
using System.Web.Mail;
using System.Text;
using System.IO;
using NUnit.Framework;
using Buzm.Email;
using System.Collections;

namespace Buzm.Email
{
    /// <summary>
    /// Allows one to send e-mail without having a configured SMTP server.
    /// </summary>
    public class DirectMailer
    {
        /// <summary>
        /// Get the Singleton instance of the Direct Mailer.
        /// </summary>
        /// <returns>The Singleton instance of the Direct Mailer.</returns>
        public static DirectMailer GetInstance ()
        {
            if (m_instance == null)
            {
                m_instance = new DirectMailer ();
            }
            return m_instance;
        }

        /// <summary>
        /// Send the specified mail message directly to the destination.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>True if the send was successful, False otherwise.</returns>
        public bool Send (MailMessage message)
        {
            DNSConfigurationQuery dnsConfigurationQuery = DNSConfigurationQuery.GetInstance ();
            return this.SendUsingDNS (message, dnsConfigurationQuery.GetDNSServers ());
        }

        private DirectMailer ()
        {
        }

        private bool SendUsingMXServer (MailMessage message, IList mxRecords)
        {
            bool sent = false;
            // Try to send to each of the mx servers in order, until one works
            // or we run out of servers
            for (int i = 0; !sent && (i < mxRecords.Count); i++)
            {
                MXRecord record = (MXRecord) mxRecords[i];
                SmtpMail.SmtpServer = record.Hostname;
                try
                {
                    SmtpMail.Send (message);
                    sent = true;
                }
                catch (System.Web.HttpException)
                {
                }
            }
            return sent;
        }

        private bool SendUsingDNS (MailMessage message, IList dnsServers)
        {
            string destinationHost = ParseHost (message.To);
            IList mxRecords = new ArrayList ();
            // Iterate over each of the DNS Servers until we get a valid
            // list of MX Servers, or until we run out of DNS servers.
            for (int i = 0; (mxRecords.Count == 0) && (i < dnsServers.Count); i++)
            {
                string dnsServer = (string) dnsServers[i];
                MXRecordQuery mxRecordQuery = new MXRecordQuery (dnsServers);
                mxRecords = mxRecordQuery.getMXRecords (destinationHost);
            }
            if (mxRecords.Count > 0)
            {
                return this.SendUsingMXServer (message, mxRecords);
            }
            return false;
        }

        /// <summary>
        /// Parse the destination host of the email address.
        /// </summary>
        /// <param name="emailAddress">The email address to parse.</param>
        /// <returns>the destination host of the email address.</returns>
        private string ParseHost (string emailAddress)
        {
            string[] addressParts = emailAddress.Split ('@');
            if ((addressParts.Length != 2) || addressParts[0].Equals (emailAddress) || addressParts[0].Equals (String.Empty))
            {
                return "";
            }
            return addressParts[1];
        }

        private static DirectMailer m_instance;

        #region NUnit Automated Test Cases

        [TestFixture, Ignore("Test times out on occasion")]
        public class DirectMailerTest
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
                DirectMailer directMailer = DirectMailer.GetInstance ();
                DirectMailer directMailer2 = DirectMailer.GetInstance ();
                Assertion.AssertSame (directMailer, directMailer2);
            }

            public void TestParseHost ()
            {
                DirectMailer directMailer = DirectMailer.GetInstance ();
                string bar = directMailer.ParseHost ("foo@bar.com");
                Assertion.AssertEquals ("bar.com", bar);

                string nada = directMailer.ParseHost ("foo@@bar.com");
                Assertion.AssertEquals ("", nada);

                nada = directMailer.ParseHost ("foobar.com");
                Assertion.AssertEquals ("", nada);

                nada = directMailer.ParseHost ("foo@twoats@bar.com");
                Assertion.AssertEquals ("", nada);
            }

            public void TestSendUsingMXServer ()
            {
                DirectMailer directMailer = DirectMailer.GetInstance ();
                MailMessage message = new MailMessage ();
                message.To = "bogus@bogus.com";
                MXRecord record = new MXRecord ("bogus.com", 1);
                ArrayList records = new ArrayList ();
                records.Add (record);
                Assertion.Assert (!directMailer.SendUsingMXServer (message, records));

                record = new MXRecord ("soaz.com", 1);
                records = new ArrayList ();
                records.Add (record);
                Assertion.Assert (!directMailer.SendUsingMXServer (message, records));

                message.To = "buzm@soaz.com";
                message.From = "buzm@soaz.com";
                message.Subject = "Hello World";
                message.Body = "Hello World";
                IList dnsServers = DNSConfigurationQuery.GetInstance ().GetDNSServers ();
                IList mxRecords = new MXRecordQuery (dnsServers).getMXRecords ("soaz.com");
                Assertion.Assert (directMailer.SendUsingMXServer (message, mxRecords));

                // Make sure we can send if we can't communicate with the first server on the list
                ArrayList mxRecords2 = new ArrayList (mxRecords);
                mxRecords2.Add (new MXRecord ("bogus", -1));
                mxRecords2.Sort ();
                Assertion.Assert (directMailer.SendUsingMXServer (message, mxRecords2));
            }

            public void TestSendUsingDNS ()
            {
                DirectMailer directMailer = DirectMailer.GetInstance ();
                MailMessage message = new MailMessage ();
                message.To = "bogus@bogus.com";
                IList dnsServersBad = new ArrayList ();
                dnsServersBad.Add ("bogus");
                Assertion.Assert (!directMailer.SendUsingDNS (message, dnsServersBad));

                // Try a successful send
                message.To = "buzm@soaz.com";
                message.From = "buzm@soaz.com";
                message.Subject = "Hello World";
                message.Body = "Hello World";
                IList dnsServersGood = DNSConfigurationQuery.GetInstance ().GetDNSServers ();
                Assertion.Assert (directMailer.SendUsingDNS (message, dnsServersGood));

                // Try with a combination of bogus and good DNS servers
                IList dnsServersMixed = new ArrayList (dnsServersBad);
                dnsServersMixed.Add (dnsServersGood[0]);
                Assertion.Assert (directMailer.SendUsingDNS (message, dnsServersMixed));
            }

            public void TestSend ()
            {
                // Try an unsuccessful send
                DirectMailer directMailer = DirectMailer.GetInstance ();
                MailMessage message = new MailMessage ();
                message.To = "bogus@bogus.com";
                Assertion.Assert (!directMailer.Send (message));

                // Try a successful send
                message.To = "buzm@soaz.com";
                message.From = "buzm@soaz.com";
                message.Subject = "See Attached";
                message.Body = "See Attached";
//                MailAttachment attachment = new MailAttachment ("C:\\dev\\Buzm\\Buzm.Network\\src\\emailsrc.zip");
//                message.Attachments.Add (attachment);
                Assertion.Assert (directMailer.Send (message));
            }
        }

        #endregion

    }
}