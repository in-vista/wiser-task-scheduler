using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Modules.Body.Interfaces;
using AutoImportServiceCore.Modules.Body.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Modules.Body.Services
{
    public class BodyService : IBodyService, IScopedService
    {
        /// <inheritdoc />
        public string GenerateBody(BodyModel bodyModel, List<int> rows, JObject resultSets)
        {
            var finalBody = new StringBuilder();

            foreach (var bodyPart in bodyModel.BodyParts)
            {
                var body = bodyPart.Text;

                // If the part needs a result set, apply it.
                if (!String.IsNullOrWhiteSpace(bodyPart.UseResultSet))
                {
                    var keyParts = bodyPart.UseResultSet.Split('.');
                    var remainingKey = keyParts.Length > 1 ? bodyPart.UseResultSet.Substring(keyParts[0].Length + 1) : "";
                    var tuple = ReplacementHelper.PrepareText(bodyPart.Text, (JObject)resultSets[keyParts[0]], remainingKey);
                    body = tuple.Item1;
                    var parameterKeys = tuple.Item2;

                    if (parameterKeys.Count > 0)
                    {
                        // Replace body with values from first row.
                        if (bodyPart.SingleItem)
                        {
                            body = ReplacementHelper.ReplaceText(body, rows, parameterKeys, (JObject)resultSets[bodyPart.UseResultSet]);
                        }
                        // Replace and combine body with values for each row.
                        else
                        {
                            body = GenerateBodyCollection(body, bodyModel.ContentType, parameterKeys, ResultSetHelper.GetCorrectObject<JArray>(bodyPart.UseResultSet, rows, resultSets));
                        }
                    }
                }

                finalBody.Append(body);
            }

            return finalBody.ToString();
        }

        /// <summary>
        /// Replace values in the body for each row and return the combined result.
        /// </summary>
        /// <param name="body">The body text to use for each row.</param>
        /// <param name="contentType">The content type that is being send in the request.</param>
        /// <param name="parameterKeys">The keys of the parameters that need to be replaced.</param>
        /// <param name="usingResultSet">The result set to get the values from.</param>
        /// <returns></returns>
        private string GenerateBodyCollection(string body, string contentType, List<string> parameterKeys, JArray usingResultSet)
        {
            var separator = String.Empty;

            // Add a separator between each row result based on content type.
            switch (contentType)
            {
                case "application/json":
                    separator = ",";
                    break;
            }

            var bodyCollection = new StringBuilder();

            // Perform the query for each row in the result set that is being used.
            for (var i = 0; i < usingResultSet.Count; i++)
            {
                var bodyWithValues = ReplacementHelper.ReplaceText(body, new List<int>() { i }, parameterKeys, (JObject)usingResultSet[i]);
                bodyCollection.Append($"{(i > 0 ? separator : "")}{bodyWithValues}");
            }

            // Add collection syntax based on content type.
            switch (contentType)
            {
                case "application/json":
                    return $"[{bodyCollection}]";
                default:
                    return bodyCollection.ToString();
            }
        }
    }
}
