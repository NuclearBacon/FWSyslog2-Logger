using System;
using System.Windows.Forms;

using System.Configuration;
using res = FWSyslog2.Properties.Resources;

namespace FWSyslog2
{
    public partial class FWSyslog2ServiceClass : Form
    {
        #region Region: Member definitions

        // none

        #endregion


        #region Region: Main methods

        /// <summary>
        /// Loads all service configuration settings not related to service installation.
        /// </summary>
        /// <remarks>
        /// Should be called each time the service is started (not just loaded) in case the configuration file was updated.
        /// Returns values:
        ///     0 if all values are loaded successfully.
        ///     1 if all values are loaded but with handled errors.
        ///     2 if one or more values were unloadable.
        /// </remarks>
        private int LoadConfigurationSettings()
        {
            // error level tracking
            int errorLevel = 0;

            // messageQueueRevisitDelay
            GetInternalOnlySetting("messageQueueRevisitDelay", ref messageQueueRevisitDelay, ref errorLevel);

            // sqlConnString
            GetConnectionString("L2DatabaseConnection", ref sqlConnString, ref errorLevel);

            // listenPort
            GetDualSourceSetting("listenPort", ref listenPort, ref errorLevel);

            // backlog stuff
            GetDualSourceSetting("backlogMaxAge", ref backlogMaxAge, ref errorLevel);
            GetDualSourceSetting("backlogMaxCount", ref backlogMaxCount, ref errorLevel);
            GetDualSourceSetting("backlogBulkSendCount", ref backlogBulkSendCount, ref errorLevel);

            // save any configuration errors to the event log
            PushConfigurationErrors();

            return errorLevel;
        }

        private void GetInternalOnlySetting(string settingName, ref int member, ref int currentErrorLevel)
        {
            try
            {
                member = Convert.ToInt32(res.ResourceManager.GetString("DefaultSetting_" + settingName));
            }
            catch (Exception e)
            {
                currentErrorLevel = 2;
                RecordConfigurationError(settingName, e, 2, true);
            }
        }

        private void GetDualSourceSetting(string settingName, ref int member, ref int currentErrorLevel)
        {
            try
            {
                member = Convert.ToInt32(ConfigurationManager.AppSettings[settingName].ToString());
            }
            catch (Exception e)
            {
                if (currentErrorLevel < 1)
                    currentErrorLevel = 1;
                
                try
                {
                    member = Convert.ToInt32(res.ResourceManager.GetString("DefaultSetting_" + settingName));
                    RecordConfigurationError(settingName, e, 1, false, member.ToString());
                }
                catch (Exception e2)
                {
                    currentErrorLevel = 2;
                    RecordConfigurationError(settingName, e, 1, false);
                    RecordConfigurationError(settingName, e2, 2, true);
                }
            }
        }

        private void GetConnectionString(string settingName, ref string member, ref int currentErrorLevel)
        {
            try
            {
                member = ConfigurationManager.ConnectionStrings[settingName].ToString();
            }
            catch (Exception e)
            {
                currentErrorLevel = 2;
                RecordConfigurationError(settingName, e, 2, false);
            }
        }

        #endregion
    }
}
