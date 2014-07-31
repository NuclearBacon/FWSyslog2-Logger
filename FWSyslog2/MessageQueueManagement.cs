using System;
using System.Data;
using System.Windows.Forms;

using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data.SqlClient;

namespace FWSyslog2
{
    public partial class FWSyslog2ServiceClass : Form
    {
        #region Region: Member definitions

        private ObservableCollection<string> messageQueue = new ObservableCollection<string>();
        private System.Threading.Timer timerMessageQueueRevist;

        #endregion


        #region Region: Main methods


        /// <summary>
        /// Event handler for when changes are made to messageQueue, specifically events where a new entry is added.
        /// </summary>
        /// <param name="sender">Required, normally provided by system, not used.</param>
        /// <param name="e">Required, normally provided by system, used to detect how the queue was changed.</param>
        /// <remarks>
        /// TODO: add backlog functionality for messages that cannot be sent to the SQL server due to some error.
        /// TODO: add configuration options to backlog (max number of messages, duration, alternate disposition, etc.)
        /// TODO: add error logging functionality
        /// TODO: add configuration option for SQL sproc name & parameters
        /// </remarks>
        private void messageQueue_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // We only handle this when new entries are added.  Changes to existing entries do not occur and we don't care when
            // entries are removed since that's what this method is doing anyway.
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // Make sure there's actually an entry in the message queue, just in case the revisit timer fires while the queue is empty.
                if (messageQueue.Count > 0)
                {
                    string currentMessage;
                    lock (messageQueue)
                    {
                        currentMessage = messageQueue[0];
                        messageQueue.RemoveAt(0);
                    }

                    // Set up a revist to the message queue if it's not empty.  This provides a small delay between locks
                    // so that inbound network message threads don't stack up while the message queue is locked for sending.
                    if (messageQueue.Count > 0)
                        timerMessageQueueRevist = new System.Threading.Timer(timerMessageQueueRevist_Callback, null, configSettings.GetValue_Int("messageQueueRevisitDelay"), Timeout.Infinite);

                    // Send the fetched message to the database.
                    try
                    {
                        using (SqlConnection sqlConnection = new SqlConnection(configSettings.GetValue_String("connString")))
                        {
                            using (SqlCommand sqlCommand = new SqlCommand("dbtest_testtablewrite", sqlConnection))
                            {
                                sqlCommand.CommandType = CommandType.StoredProcedure;
                                sqlCommand.Parameters.Add("@p_Input", SqlDbType.VarChar).Value = currentMessage;

                                sqlConnection.Open();
                                sqlCommand.ExecuteNonQuery();
                            }
                        }
                    }
                    catch (SqlException sqlException)
                    {
                        throw sqlException;
                    }

                }
                else
                {
                    // The queue is empty so the revisit timer shouldn't be running; this makes sure it isn't.
                    timerMessageQueueRevist.Dispose();
                }
            }
        }

        /// <summary>
        /// Event handler for timerMessageQueueRevist timeouts.
        /// </summary>
        /// <param name="state">Required, normally provided by system, not used.</param>
        /// <remarks>
        /// This method simply loops back to a new messageQueue_CollectionChanged event after a small delay.
        /// </remarks>
        private void timerMessageQueueRevist_Callback(Object state)
        {
            messageQueue_CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
        }

        #endregion
    }
}

