using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using AutoImportServiceCore.Core.Models;

namespace AutoImportServiceCore.Modules.HttpApis.Models
{
    /// <summary>
    /// A model for a HTTP API.
    /// </summary>
    public class HttpApiModel : ActionModel
    {
        /// <summary>
        /// Gets or sets the full URL for the call.
        /// </summary>
        [Required]
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the method to use.
        /// </summary>
        [Required]
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets additional headers to add before sending the request.
        /// </summary>
        public HeaderModel[] Headers { get; set; } = Array.Empty<HeaderModel>();

        /// <summary>
        /// Gets or sets the body to send with the request.
        /// </summary>
        public BodyModel Body { get; set; }
    }
}
