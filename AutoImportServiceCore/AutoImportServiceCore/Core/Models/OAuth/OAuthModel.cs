using System;
using System.Xml.Serialization;

namespace AutoImportServiceCore.Core.Models.OAuth
{
    [XmlType("OAuth")]
    public class OAuthModel
    {
        /// <summary>
        /// Gets or sets the name of the API which is used to target it in an HTTP API request.
        /// </summary>
        public string ApiName { get; set; }

        /// <summary>
        /// Gets or sets the URL to the endpoint where the OAuth needs to be done.
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the username to login with when no access token or refresh token is available.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password to login with when no access token or refresh token is available.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the log settings.
        /// </summary>
        public LogSettings LogSettings { get; set; }

        /// <summary>
        /// Gets or sets custom key value pairs to include in the form.
        /// </summary>
        [XmlArray("FormKeyValues")]
        [XmlArrayItem(typeof(FormKeyValueModel))]
        public FormKeyValueModel[] FormKeyValues { get; set; }

        /// <summary>
        /// Gets or sets the access token.
        /// </summary>
        [XmlIgnore]
        public string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the type of the token.
        /// </summary>
        [XmlIgnore]
        public string TokenType { get; set; }

        /// <summary>
        /// Gets or sets the refresh token.
        /// </summary>
        [XmlIgnore]
        public string RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the time the access token expires on.
        /// </summary>
        [XmlIgnore]
        public DateTime ExpireTime { get; set; }
    }
}
