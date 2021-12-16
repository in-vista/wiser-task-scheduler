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
        /// Gets or sets the body parts.
        /// </summary>
        public BodyPartModel[] BodyParts { get; set; }
    }
}
