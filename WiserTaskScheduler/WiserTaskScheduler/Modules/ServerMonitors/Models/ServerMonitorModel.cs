using System;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using WiserTaskScheduler.Core.Models;
using GeeksCoreLibrary.Core.Enums;
using GeeksCoreLibrary.Modules.Communication.Enums;
using WiserTaskScheduler.Modules.ServerMonitors.Enums;

namespace WiserTaskScheduler.Modules.ServerMonitors.Models
{
    [XmlType("ServerMonitor")]
    public class ServerMonitorModel : ActionModel
    {
        public int threshold { get; set; }

        public CommunicationTypes communicationType { get; set; } = CommunicationTypes.Email;

        public ServerMonitorType type { get; set; }


        public string EmailAddressForErrorWarning { get; set; }



    }
}
