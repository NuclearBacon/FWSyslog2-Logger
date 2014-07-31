using System;
using System.Windows.Forms;
using System.Diagnostics;

using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml;
using res = FWSyslog2.Properties.Resources;

namespace FWSyslog2
{
    public partial class FWSyslog2ServiceClass : Form
    {
        #region Region: Member definitions

        private static ConfigurationSettings configSettings;

        #endregion


        #region Region: Main methods



        #endregion


        
        #region Region: Error Logging

        private struct ConfigurationErrorRecord
        {
            public string settingName;
            public Exception exceptionDetails;
            public int errorLevel;
            public bool internalSetting;
            public string loadedDefault;
        }

        /// <summary>
        /// Records the details of errors that occur during the loading of application settings.
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
            configurationErrorQueue.Add(new ConfigurationErrorRecord()
            {
                settingName = settingName,
                exceptionDetails = exceptionDetails,
                errorLevel = errorLevel,
                internalSetting = internalSetting,
                loadedDefault = loadedDefault
            });

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


        #region Region: Utility sub-classes.

        /// <summary>
        /// Loader/container class for application settings loaded from App.config and internal defaults.  
        /// Service [re]starts after a failed settings load should attempt to reload the settings by 
        /// simply creating a new instance of this class.
        /// </summary>
        private class ConfigurationSettings
        {
            private List<configMember> configMembers = new List<configMember>();
            public bool LoadValidated;

            /// <summary>
            /// Class contructor.
            /// </summary>
            public ConfigurationSettings()
            {
                LoadValidated = false;  // should be set to True by following method, if all goes well.
                LoadConfigSettings();
            }

            /// <summary>
            /// Loads all service configuration settings not related to service installation.
            /// </summary>
            private void LoadConfigSettings()
            {
                List<configMemberLoadHelper> loadHelpers = new List<configMemberLoadHelper>();
                string internalReadResult = null;
                XmlDocument doc = new XmlDocument();
                XmlNodeList nodes = null;
                bool criticalLoadFailure = false;

                // Load the embedded configuration settings definition file.
                try
                {
                    using (Stream stream = this.GetType().Assembly.GetManifestResourceStream("FWSyslog2.ConfigurationProfile.xml"))
                    {
                        using (StreamReader sr = new StreamReader(stream))
                        {
                            internalReadResult = sr.ReadToEnd();
                        }
                    }
                }
                catch (Exception e)
                {
                    throw e;
                    //TODO: Add critical internal failure handling.
                }


                // Dump the loaded file in to an XML document.
                if (internalReadResult != null)
                {
                    try
                    {
                        doc.LoadXml(internalReadResult);
                        nodes = doc.DocumentElement.SelectNodes("/configurationMembers/member");
                    }
                    catch (XmlException e)
                    {
                        throw e;
                        //TODO: Add critical internal failure handling.
                    }
                }


                // Parse the XML records in to local members.
                if (nodes.Count > 0)
                {
                    foreach (XmlNode node in nodes)
                    {
                        configMemberLoadHelper cmlh = new configMemberLoadHelper();
                        try
                        {
                            cmlh.memberName = node.Attributes["name"].Value;
                            cmlh.internalValue = node.SelectSingleNode("internalValue").InnerText;
                            switch (node.SelectSingleNode("externalValueType").InnerText)
                            {
                                case "Expected":
                                    cmlh.externalExpected = true;
                                    break;
                                case "Optional":
                                    cmlh.externalOptional = true;
                                    break;
                                case "ConnectionString":
                                    cmlh.isConnectionString = true;
                                    cmlh.externalExpected = true;
                                    break;
                            }
                        }
                        catch (XmlException e)
                        {
                            cmlh.internalLoadException = (Exception)e;
                        }
                        loadHelpers.Add(cmlh);
                    }
                }
                else
                {
                    //TODO: Add critical internal failure handling. (if we get here, the Stream->XML Doc conversion above failed silently)
                }


                // Load external values.
                foreach (configMemberLoadHelper cmlh in loadHelpers)
                {
                    if (cmlh.externalExpected || cmlh.externalOptional)
                    {
                        try
                        {
                            if (cmlh.isConnectionString)
                                cmlh.externalValue = ConfigurationManager.ConnectionStrings[cmlh.memberName].ToString();
                            else
                                cmlh.externalValue = ConfigurationManager.AppSettings[cmlh.memberName].ToString();
                        }
                        catch (Exception e)
                        {
                            cmlh.externalLoadException = e;
                        }
                    }
                }


                // Load error checking, preferred value selection and error reporting.  Logic soup.
                foreach (configMemberLoadHelper cmlh in loadHelpers)
                {
                    // Optional/expected external with a good external load.
                    if ((cmlh.externalOptional || cmlh.externalExpected) && cmlh.externalValue != null && cmlh.internalLoadException == null)
                    {
                        cmlh.selectedValue = cmlh.externalValue;
                        // It wasn't needed, but check for errors while loading the internal value for reporting purposes.
                        if ((cmlh.internalValue == null && !cmlh.isConnectionString) || cmlh.internalLoadException != null)
                            RecordConfigurationError(cmlh.memberName, cmlh.internalLoadException, 1, true);
                    }

                    // Expected external with a bad external load but with a good internal load to fall back on.
                    else if (cmlh.externalExpected && cmlh.internalValue != null && cmlh.internalLoadException == null)
                    {
                        cmlh.selectedValue = cmlh.internalValue;
                        RecordConfigurationError(cmlh.memberName, cmlh.externalLoadException, 1, false, cmlh.selectedValue);
                    }

                    // Expected external with bad internal and external loads.
                    else if (cmlh.externalExpected)
                    {
                        RecordConfigurationError(cmlh.memberName, cmlh.externalLoadException, 1, false);
                        RecordConfigurationError(cmlh.memberName, cmlh.internalLoadException, 2, true);
                        criticalLoadFailure = true;
                    }

                    // Good internal loads or failed optional external loads with a good internal load to fall on.
                    else if (cmlh.internalValue != null && cmlh.internalLoadException == null)
                    {
                        cmlh.selectedValue = cmlh.internalValue;
                    }

                    // Everything else, which should only be internal only settings with failed internal loads with/without failed optional externals.
                    else
                    {
                        RecordConfigurationError(cmlh.memberName, cmlh.internalLoadException, 2, true);
                        criticalLoadFailure = true;
                    }
                }

                // Wrap up.
                if (!criticalLoadFailure)
                {
                    foreach (configMemberLoadHelper cmlh in loadHelpers)
                    {
                        configMember cm = new configMember();

                        cm.memberName = cmlh.memberName;
                        cm.memberValue = cmlh.selectedValue;

                        configMembers.Add(cm);
                    }
                    LoadValidated = true;
                }

                int g = 1;
            }

            /// <summary>
            /// Returns the value for 'int' based settings loaded from config files.  Error handling should be
            /// done by the calling method.
            /// </summary>
            /// <param name="memberName">The  name of the setting.</param>
            /// <returns></returns>
            public int GetValue_Int(string memberName)
            {
                return Convert.ToInt32((configMembers.Find(cm => cm.memberName == memberName).memberValue));
            }

            /// <summary>
            /// Returns the value for 'string' based settings loaded from config files.  Error handling should be
            /// done by the calling method.
            /// <param name="memberName">The  name of the setting.</param>
            /// <returns></returns>
            public string GetValue_String(string memberName)
            {
                return configMembers.Find(cm => cm.memberName == memberName).memberValue;
            }


            /// <summary>
            /// Helper class; holds temporary data for multiple loaded settings before those settings are processed.
            /// </summary>
            private class configMemberLoadHelper
            {
                public string memberName;
                
                private string _internalValue;
                public string internalValue
                {
                    get { return _internalValue; }
                    set { if (value == "") _internalValue = null; else _internalValue = value; }
                }

                public bool externalExpected = false;
                public bool externalOptional = false;
                public bool isConnectionString = false;
                public string externalValue;

                public Exception internalLoadException = null;
                public Exception externalLoadException = null;

                public string selectedValue;
            }

            /// <summary>
            /// Container class that represents a loaded configuration setting.
            /// </summary>
            private class configMember
            {
                public string memberName;
                public string memberValue;
            }
        }

        #endregion
    }
}
