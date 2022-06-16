using System.Xml.Serialization;
using AutoImportServiceCore.Core.Models;

namespace AutoImportServiceCore.Modules.CleanupItems.Models
{
    [XmlType("CleanupItem")]
    public class CleanupItemModel : ActionModel
    {
        /// <summary>
        /// Gets or sets the name of the entity.
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// Gets or sets the number of days before the action needs to be performed on the items of the given entity.
        /// </summary>
        public int NumberOfDaysToStore { get; set; }

        /// <summary>
        /// Gets or sets if the "changed_on" column needs to be used instead of the "added_on" column.
        /// </summary>
        public bool SinceLastChange { get; set; }

        /// <summary>
        /// Gets or sets if the action needs to be performed on the items of the given entity in the archive tables.
        /// </summary>
        public bool FromArchive { get; set; }

        /// <summary>
        /// Gets or sets the connection string to use for the cleanup of this entity to manage multiple customers from a single AIS.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the log settings.
        /// </summary>
        public LogSettings LogSettings { get; set; }
    }
}