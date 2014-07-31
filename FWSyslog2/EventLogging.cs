using System.Windows.Forms;
using System.Diagnostics;
using System;
using System.Collections.Generic;

using res = FWSyslog2.Properties.Resources;

/**********************************************************************************************
 * TODO/WIP:  Possibly move regions dedicated to other modules back to those modules.  This module
 * may get pretty messy and debugging may be easier with local error handling instead of this.
 * *******************************************************************************************/

namespace FWSyslog2
{
    public partial class FWSyslog2ServiceClass : Form
    {
        #region Region: Member definitions

        private static List<ConfigurationErrorRecord> configurationErrorQueue = new List<ConfigurationErrorRecord>();

        #endregion


        #region Region: Main methods

        /// <summary>
        /// Sets up a source for the application in the Windows Event Log, if one doesn't exist.
        /// </summary>
        private bool ConfigureEventLog()
        {
            bool result;

            try
            {
                if (!EventLog.SourceExists(res.WindowsEventLog_AppName))
                    EventLog.CreateEventSource(res.WindowsEventLog_AppName, res.WindowsEventLog_GroupName);
                result = true;
            }
            catch (Exception e)
            {
#               if DEBUG
                    throw new Exception("The application hasn't created an Event Log source yet.  The application must be run while Visual Studio has elevated permissions at least once to do this.");
                    Application.Exit(); // Because the application sometimes likes to randomly ignore throw statements.  Great, huh?
#               else
                    // The service is somehow being run without admin rights or explicity being denied access to [some elements of] the Event Log
                    // service.  Since the application source hasn't already been created in the Event Log, there's not much that can be done.
                
                    result = false;
#               endif
            }

            return result;
        }

        #endregion



        #region Region: Network events

        private static void RecordNetworkError(Exception exceptionDetails, int errorLevel)
        {
            // This would all be handled by the last overload below, so just point there with an empty errorData parameter.
            RecordNetworkError(exceptionDetails, errorLevel, new byte[]{});
        }

        private static void RecordNetworkError(Exception exceptionDetails, int errorLevel, string errorData)
        {
            // Strip down the string error data in to bytes and pass it to the right overload.
            byte[] bytes = new byte[errorData.Length * sizeof(char)];
            System.Buffer.BlockCopy(errorData.ToCharArray(), 0, bytes, 0, bytes.Length);

            RecordNetworkError(exceptionDetails, errorLevel, bytes);
        }

        private static void RecordNetworkError(Exception exceptionDetails, int errorLevel, byte[] errorData)
        {

        }


        #endregion
        

    }
}

