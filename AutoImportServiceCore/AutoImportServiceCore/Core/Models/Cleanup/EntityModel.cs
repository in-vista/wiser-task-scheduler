using System.Xml.Serialization;

namespace AutoImportServiceCore.Core.Models.Cleanup
{
    [XmlType("Entity")]
    public class EntityModel
    {
        /// <summary>
        /// Gets or sets the name of the entity.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets what action the cleanup service needs to perform.
        /// </summary>
        public CleanupActions CleanupAction { get; set; }

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
        /// Gets or sets the log settings.
        /// </summary>
        public LogSettings LogSettings { get; set; }
    }
}