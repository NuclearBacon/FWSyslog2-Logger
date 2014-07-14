using System.Windows.Forms;
using System.Diagnostics;
using System;
using System.Collections.Generic;

using res = FWSyslog2.Properties.Resources;

/**********************************************************************************************
 * TODO:  Possibly move regions dedicated to other modules back to those modules.  This module
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


        #region Region: Configuration load events

        /// <summary>
        /// Accepts error event details when loading application settings.
        /// </summary>
        /// <param name="errorDetails">An explanation of the error, typically the <code>Message</code> member of an <code>Exception</code> object.</param>
        /// <param name="errorLevel"></param>
        /// <param name="failedExternalLoad"></param>
        /// <param name="failedInternalLoad"></param>
        /// <remarks>
        /// Method is intended to queue up all errors in to a single event.  This prevents spamming the event log for each setting
        /// that failed to load, instead creating a single event of appropriate severity with the details about all failures together.
        /// </remarks>
        private static void RecordConfigurationError(string settingName, Exception exceptionDetails, int errorLevel, bool internalSetting, string loadedDefault = "")
        {
            configurationErrorQueue.Add(new ConfigurationErrorRecord() { settingName = settingName,
                                                                        exceptionDetails = exceptionDetails, 
                                                                        errorLevel = errorLevel, 
                                                                        internalSetting = internalSetting, 
                                                                        loadedDefault = loadedDefault });
                
        }

        /// <summary>
        /// Compiles the errors recorded during configuration load and sends them to the event log.
        /// </summary>
        private static void PushConfigurationErrors()
        {
            if (configurationErrorQueue.Count > 0)
            {
                string tempErrorBundle = "";
                int tempMaxErrorLevel = 0;

                foreach (ConfigurationErrorRecord cer in configurationErrorQueue)
                {
                    string errorDetails;

                    // Load context based error messaging.
                    if (cer.exceptionDetails.GetType() == typeof(FormatException))
                        errorDetails = res.WindowsEventLog_ConfigError_MalformedData;
                    else if (cer.exceptionDetails.GetType() == typeof(NullReferenceException))
                        errorDetails = res.WindowsEventLog_ConfigError_AppConfigMissingEntry;
                    else if (cer.exceptionDetails.GetType() == typeof(System.Configuration.ConfigurationErrorsException))
                        errorDetails = res.WindowsEventLog_ConfigError_AppConfigFormatError;
                    else
                        errorDetails = cer.exceptionDetails.Message;

                    // Mix all the details for this error together and add them to the overall error text.
                    tempErrorBundle += string.Format("{0}: '{1}' could not be loaded from {2}: {3}.{4}\r\n\r\n",
                        cer.errorLevel == 1 ? "WARNING" : "CRITICAL",
                        cer.settingName,
                        cer.internalSetting ? "default settings" : "App.config",
                        errorDetails,
                        cer.loadedDefault == "" ? "" : " Loaded default value: " + cer.loadedDefault);

                    // Error severity tracking.
                    if (cer.errorLevel > tempMaxErrorLevel)
                        tempMaxErrorLevel = cer.errorLevel;
                }

                // Tack on the appropriate preface for the error severities.
                tempErrorBundle = ((tempMaxErrorLevel == 1) ? res.WindowsEventLog_ConfigError_WarningPreface : res.WindowsEventLog_ConfigError_CriticalPreface)
                    + "\r\n\r\n\r\n" + tempErrorBundle;

                // The actual event logging.
                EventLog.WriteEntry(res.WindowsEventLog_AppName,
                                    tempErrorBundle,
                                    tempMaxErrorLevel == 1 ? EventLogEntryType.Warning : EventLogEntryType.Error,
                                    1,
                                    (short)tempMaxErrorLevel);
            }
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
        

        #region Region: Constructs

        private struct ConfigurationErrorRecord
        {
            public string settingName;
            public Exception exceptionDetails;
            public int errorLevel;
            public bool internalSetting;
            public string loadedDefault;
        }

        #endregion

    }
}
