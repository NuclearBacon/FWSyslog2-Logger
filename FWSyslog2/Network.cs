using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace FWSyslog2
{
    public partial class FWSyslog2ServiceClass : Form
    {
        #region Region: Member definitions

        private Thread listenerThread;

        private IPAddress syslogHost;
        private bool enforceHostMatch;
        private bool hostIsDefaultGateway;

        private UdpClient udpClient;
        private int listenPort;
        private int openListenerMaxAttempts;
        private int openListenerRetryDelay;

        #endregion


        #region Region: Main methods

        /// <summary>
        /// Sets up the UDP client to listen for a new message from the Syslog sender.
        /// </summary>
        /// <remarks>
        /// Initially called by the service start method, then looped thereafter by ListenerCallbackHander(...).
        /// Any error in this method is a critial error and is handled by the service start method calling it.
        /// </remarks>
        private void Listener()
        {
            // Fetch configurables.
            try
            {
                listenPort = configSettings.GetValue_Int("listenPort");
                openListenerMaxAttempts = configSettings.GetValue_Int("openListenerMaxAttempts");
                openListenerRetryDelay = configSettings.GetValue_Int("openListenerRetryDelay");
                enforceHostMatch = configSettings.GetValue_Boolean("enforceHostMatch");

                hostIsDefaultGateway = false;
                if (enforceHostMatch)
                {
                    string tmpHost = configSettings.GetValue_String("syslogHost");
                    if (tmpHost == "0")
                        hostIsDefaultGateway = true;
                    else
                    {
                        try
                        {
                            syslogHost = IPAddress.Parse(tmpHost);
                        }
                        catch
                        {
                            try
                            {
                                syslogHost = Dns.GetHostAddresses(tmpHost).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                            }
                            catch (Exception e)
                            {
                                throw e;
                            }
                        }

                    }
                }
                else
                    syslogHost = null;
            }
            catch (Exception e)
            {
                throw e;
            }

            // Setup and start the UDP client.
            udpClient = new UdpClient(listenPort);
            openListenerSocket();
        }


        /// <summary>
        /// Event handler for udpClient's receipt of a message.
        /// </summary>
        /// <param name="result">Passed by system.</param>
        /// <remarks>
        /// TODO: update message queue addition to include message info (local timestamp, sender port/ip info)
        /// </remarks>
        private void ListenerCallbackHandler(IAsyncResult result)
        {
            // Collect sender and message data.
            IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, listenPort);
            byte[] receivedData = udpClient.EndReceive(result, ref remoteIpEndPoint);


            // Set updClient back up for next message.
            if (!serviceIsStopping)
            {
                openListenerSocket();
            }


            // Collect/validate sender info.
            bool hostValidated = false;
            int hostPort = remoteIpEndPoint.Port;
            IPAddress hostIP = remoteIpEndPoint.Address;

            if (enforceHostMatch)
            {
                if (hostIsDefaultGateway)
                {
                    // Default gateway is checked with each message instead of being stored as it could potentially change IPs between messages.
                    if (IPAddress.Equals(hostIP, GetDefaultGateway()))
                        hostValidated = true;
                }
                else
                {
                    if (IPAddress.Equals(hostIP, syslogHost))
                        hostValidated = true;
                }
            }
            else
                hostValidated = true;


            // Pass the data collected from this message to the outbound queue.
            if (hostValidated)
            {
                lock (messageQueue)
                {
                    messageQueue.Add(Encoding.ASCII.GetString(receivedData));
                }
                lock (TEMP_messageQueueVisualizer)
                {
                    TEMP_messageQueueVisualizer.Add(string.Format("The following message was {0}validated:", hostValidated ? "" : "not "));
                    TEMP_messageQueueVisualizer.Add(Encoding.ASCII.GetString(receivedData));
                }
            }
        }


        /// <summary>
        /// Attempts to open the listener socket and set up the callback handler.
        /// </summary>
        /// <remarks>
        /// TODO:  Add service stop call when the socket cannot be opened.
        /// </remarks>
        private bool openListenerSocket()
        {
            int openAttempt = 1;
            bool openSuccess = false;

            while (!openSuccess && openAttempt <= openListenerMaxAttempts)
            {
                try
                {
                    udpClient.BeginReceive(new AsyncCallback(ListenerCallbackHandler), null);
                    openSuccess = true;
                }
                catch (Exception e)
                {
                    openAttempt++;
                    if (openAttempt > openListenerMaxAttempts)
                    {
                        serviceIsStopping = true;
                        // service stop call
                    }
                    else
                        Thread.Sleep(openListenerRetryDelay);
                }
            }

            return openSuccess;
        }


        /// <summary>
        /// Gets the IP address of the default gateway for the computer running the service.
        /// </summary>
        /// <remarks>
        /// Used to ignore messages not sent by the default gateway, which is also the Syslog sender in the development environment.
        /// Error handling should be done by the calling function.
        /// </remarks>
        private IPAddress GetDefaultGateway()
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault();
            if (nic == null)
            {
                throw new Exception("No network devices detected.");
            }
            return nic.GetIPProperties().GatewayAddresses.FirstOrDefault().Address;
        }

        #endregion
    }
}
