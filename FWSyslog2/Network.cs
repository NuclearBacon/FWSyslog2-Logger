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

        private int listenPort;
        private UdpClient udpClient;
        private IPAddress defaultGateway;
        private Thread listenerThread;

        #endregion


        #region Region: Main methods

        /// <summary>
        /// Gets the IP address of the default gateway for the computer running the service.
        /// </summary>
        /// <remarks>
        /// Used to ignore messages not sent by the default gateway, which is also the Syslog sender in the development environment.
        /// TODO: add error logging functionality
        /// </remarks>
        private IPAddress GetDefaultGateway()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault();
                if (nic == null)
                {
                    RecordNetworkError(null, 1, "No network adapters detected.");
                    return null;
                }
                return nic.GetIPProperties().GatewayAddresses.FirstOrDefault().Address;
            }
            catch (Exception e)
            {
                RecordNetworkError(e, 2);
                return null;
            }
        }


        /// <summary>
        /// Sets up the UDP client to listen for a new message from the Syslog sender.
        /// </summary>
        /// <remarks>
        /// Initially called by the service start method, then looped thereafter by ListenerCallbackHander(...).
        /// TODO: expand error handling
        /// </remarks>
        private void Listener()
        {
            if (udpClient == null)
            {
                try
                {
                    udpClient = new UdpClient(listenPort);
                    udpClient.BeginReceive(new AsyncCallback(ListenerCallbackHandler), null);
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
        }

        /// <summary>
        /// Event handler for udpClient's receipt of a message.
        /// </summary>
        /// <param name="result"></param>
        /// <remarks>
        /// TODO: add improved error handling.
        /// TODO: add support for service shutdown initiation between a callback being received and this method firing.
        /// TODO: add option to enforce or ingore sender/gateway matchup.
        /// TODO: add option to specify an alternate sender IP address.
        /// </remarks>
        private void ListenerCallbackHandler(IAsyncResult result)
        {
            // Collect sender and message data.
            IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, listenPort);
            byte[] receivedData = udpClient.EndReceive(result, ref remoteIpEndPoint);

            // Set updClient back up for next message.
            if (!serviceIsStopping)
            {
                try
                {
                    udpClient.BeginReceive(new AsyncCallback(ListenerCallbackHandler), null);
                }
                catch (Exception e)
                {
                    throw e;
                }
            }

            // Pass the data collected from this message to the outbound queue.
            lock (messageQueue)
            {
                messageQueue.Add(Encoding.ASCII.GetString(receivedData));
            }
            lock (messageQueueVisualizer)
            {
                messageQueueVisualizer.Add(Encoding.ASCII.GetString(receivedData));
            }
        }

        #endregion
    }
}
