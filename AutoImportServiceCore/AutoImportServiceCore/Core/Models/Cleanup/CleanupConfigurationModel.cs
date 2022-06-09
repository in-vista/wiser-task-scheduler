using System.Xml.Serialization;

namespace AutoImportServiceCore.Core.Models.Cleanup
{
    [XmlRoot("CleanupConfiguration")]
    public class CleanupConfigurationModel
    {
        /// <summary>
        /// Gets or sets the entities.
        /// </summary>
        [XmlArray("Entities")]
        [XmlArrayItem(typeof(EntityModel))]
        public EntityModel[] Entities { get; set; }
        
        /// <summary>
        /// Gets or sets the log settings.
        /// </summary>
        public LogSettings LogSettings { get; set; }
    }
}