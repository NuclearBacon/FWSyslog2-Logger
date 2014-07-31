using System;
using System.Collections.Generic;
using System.Windows.Forms;

using System.Threading;

namespace FWSyslog2
{
    public partial class FWSyslog2ServiceClass : Form
    {
        #region Region: Member definitions

        // service status
        bool serviceIsStopping = false;

        // TEMP
        private List<string> TEMP_messageQueueVisualizer = new List<string>();
        private bool TEMP_EventLogResults;

        #endregion


        #region Region: Base service methods

        public FWSyslog2ServiceClass()
        {
            InitializeComponent();

            TEMP_EventLogResults = ConfigureEventLog();

            if (!ConfigureEventLog())       // If this fails we cannot access the Event log.  Abort startup.
            {
            }

            messageQueue.CollectionChanged += messageQueue_CollectionChanged;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // This method is serving as our testing Service:OnStart() call.

            configSettings = new ConfigurationSettings();

            if (!TEMP_EventLogResults)
            {
                textBox1.AppendText("There was an error setting up access to the event log.  Suck.");  // remove this
                //this.ExitCode = 1297;     // http://msdn.microsoft.com/en-us/library/ms681383(v=vs.85)
                //this.Stop();
            }
            else if (!configSettings.LoadValidated)
            {
                textBox1.AppendText("There was an error loading configuration settings.  The error could not be recovered from.  The event log should be checked for more details.");  // remove this
                //this.ExitCode = 1610;     // http://msdn.microsoft.com/en-us/library/ms681385(v=vs.85)
                //this.Stop();
            }
            else
            {
                listenerThread = new Thread(new ThreadStart(Listener));
                listenerThread.IsBackground = false;
                listenerThread.Start();

                System.Windows.Forms.Timer timTemp = new System.Windows.Forms.Timer();
                timTemp.Interval = 50;
                timTemp.Tick += timTemp_Tick;
                timTemp.Start();
            }
        }



        // ======================== TEMP VISUALIZER == REMOVE THIS =========================
        void timTemp_Tick(object sender, EventArgs e)
        {
            lock (TEMP_messageQueueVisualizer)
            {
                if (TEMP_messageQueueVisualizer.Count > 0)
                {
                    textBox1.AppendText(TEMP_messageQueueVisualizer[0] + "\r\n");
                    TEMP_messageQueueVisualizer.RemoveAt(0);
                }
            }
        }

        #endregion
    }
}
