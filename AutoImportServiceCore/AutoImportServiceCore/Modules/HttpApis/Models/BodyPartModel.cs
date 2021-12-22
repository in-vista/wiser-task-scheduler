using System.ComponentModel.DataAnnotations;

namespace AutoImportServiceCore.Modules.HttpApis.Models
{
    /// <summary>
    /// A model for a part of the body of a HTTP API call.
    /// </summary>
    public class BodyPartModel
    {
        /// <summary>
        /// Gets or sets the text of the body.
        /// </summary>
        [Required]
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets if the body part is added once or for each row in the result set.
        /// </summary>
        public bool SingleItem { get; set; } = true;

        /// <summary>
        /// Gets or sets the result set to use for this specific part.
        /// </summary>
        public string UseResultSet { get; set; }
    }
}
