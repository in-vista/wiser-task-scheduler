using AutoImportServiceCore.Core.Models;

namespace AutoImportServiceCore.Modules.Queries.Models
{
    /// <summary>
    /// A model for a query.
    /// </summary>
    public class QueryModel : ActionModel
    {
        /// <summary>
        /// Gets or sets the query to execute.
        /// </summary>
        public string Query { get; set; }
    }
}
