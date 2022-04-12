using System.Xml.Serialization;

namespace AutoImportServiceCore.Core.Models.OAuth
{
    [XmlType("FormKeyValue")]
    public class FormKeyValueModel
    {
        /// <summary>
        /// Gets or sets the key to include in the form.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the value to include in the form.
        /// </summary>
        public string Value { get; set; }
    }
}
