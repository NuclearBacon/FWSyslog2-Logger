using System;
using System.ServiceProcess;
using System.Configuration.Install;
using System.ComponentModel;
using rs = FWSyslog2.Properties.Resources;

namespace FWSyslog2
{
    [RunInstaller(true)]
    public class FWSyslogInstaller : Installer
    {
        private ServiceProcessInstaller processInstaller;
        private ServiceInstaller serviceInstaller;

        public FWSyslogInstaller()
        {
            processInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            processInstaller.Account = ServiceAccount.LocalSystem;
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = rs.Service_Name;
            serviceInstaller.Description = rs.Service_Description;
            serviceInstaller.DisplayName = rs.Service_Name;

            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }
    }
}
