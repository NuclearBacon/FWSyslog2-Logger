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

        // database
        private string sqlConnString;

        // TEMP
        private List<string> messageQueueVisualizer = new List<string>();

        #endregion


        #region Region: Base service methods

        public FWSyslog2ServiceClass()
        {
            InitializeComponent();
            messageQueue.CollectionChanged += messageQueue_CollectionChanged;

            ConfigureEventLog();
            LoadConfigurationSettings();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            /*
            listenerThread = new Thread(new ThreadStart(Listener));
            listenerThread.IsBackground = false;
            listenerThread.Start();

            System.Windows.Forms.Timer timTemp = new System.Windows.Forms.Timer();
            timTemp.Interval = 50;
            timTemp.Tick += timTemp_Tick;
            timTemp.Start();
            */
        }



        // ======================== TEMP VISUALIZER == REMOVE THIS =========================
        void timTemp_Tick(object sender, EventArgs e)
        {
            lock (messageQueueVisualizer)
            {
                if (messageQueueVisualizer.Count > 0)
                {
                    textBox1.AppendText(messageQueueVisualizer[0] + "\r\n");
                    messageQueueVisualizer.RemoveAt(0);
                }
            }
        }

        #endregion
    }
}
