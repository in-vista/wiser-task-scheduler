namespace AutoImportServiceCore.Modules.HttpApis.Models
{
    /// <summary>
    /// A model to add a body to a HTTP API call.
    /// </summary>
    public class BodyModel
    {
        /// <summary>
        /// Gets or sets the type of the content that will be send to the API.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the body.
        /// If a result set is used and there are parameters the body will be filled with the values from the rows and send as an array instead.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// Gets or sets if only a single item needs to be send.
        /// When a result set is provided only the first row will be used to fill the data.
        /// </summary>
        public bool SingleItem { get; set; }
    }
}
