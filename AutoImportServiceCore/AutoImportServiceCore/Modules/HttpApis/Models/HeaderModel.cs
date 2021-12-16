namespace AutoImportServiceCore.Modules.HttpApis.Models
{
    /// <summary>
    /// A model to add Headers to a HTTP API call.
    /// </summary>
    public class HeaderModel
    {
        /// <summary>
        /// Gets or sets the name of the header property to set.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the value of the header property to set.
        /// </summary>
        public string Value { get; set; }
    }
}
