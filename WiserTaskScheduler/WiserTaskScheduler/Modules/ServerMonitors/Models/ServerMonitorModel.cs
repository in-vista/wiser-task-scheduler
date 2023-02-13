using System.Xml.Serialization;
using WiserTaskScheduler.Core.Models;
using GeeksCoreLibrary.Modules.Communication.Enums;
using WiserTaskScheduler.Modules.ServerMonitors.Enums;
using GeeksCoreLibrary.Modules.Communication.Models;


namespace WiserTaskScheduler.Modules.ServerMonitors.Models
{
    [XmlType("ServerMonitor")]
    public class ServerMonitorModel : ActionModel
    {
        /// <summary>
        /// Gets or sets the Threshold to use for the monitor check.
        /// </summary>
        public int Threshold { get; set; }

        /// <summary>
        /// Gets or sets the type of communication to process.
        /// </summary>
        public CommunicationTypes CommunicationType { get; set; } = CommunicationTypes.Email;

        /// <summary>
        /// Gets or sets the type of Server Monitor.
        /// </summary>
        public ServerMonitorTypes ServerMonitorType { get; set; }

        /// <summary>
        /// Gets or sets the email to send the warning to.
        /// </summary>
        public string EmailAddressForWarning { get; set; }

        /// <summary>
        /// Gets or sets the type of communication to process.
        /// </summary>
        public SmtpSettings SmtpSettings { get; set; }
    }
}
