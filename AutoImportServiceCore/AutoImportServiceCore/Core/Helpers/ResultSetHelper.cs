using System;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Core.Helpers
{
    public static class ResultSetHelper
    {
        /// <summary>
        /// Get the correct object based on the key.
        /// </summary>
        /// <typeparam name="T">The type of JToken that needs to be returned.</typeparam>
        /// <param name="key">The key, separated by comma, to the required object.</param>
        /// <param name="row">The index/row of the array, to be used if '[i]' is used in the key.</param>
        /// <param name="usingResultSet">The result set from where to start te search.</param>
        /// <returns></returns>
        public static T GetCorrectObject<T>(string key, int row, JObject usingResultSet) where T : JToken
        {
            var keyParts = key.Split(".");

            // No next step left, return object as requested type.
            if (keyParts.Length == 1)
            {
                return (T)usingResultSet[keyParts[0]];
            }

            var remainingKey = key.Substring(key.IndexOf(".") + 1);

            // Object to step into is not an array.
            if (!keyParts[0].EndsWith("]"))
            {
                return GetCorrectObject<T>(remainingKey, row, (JObject)usingResultSet[keyParts[0]]);
            }

            var index = row;
            // If a specific value is used for the array index use that instead.
            if (keyParts[0][keyParts[0].Length - 2] != 'i')
            {
                var indexIdentifier = keyParts[0].Substring(keyParts[0].IndexOf('['));
                index = Int32.Parse(indexIdentifier.Substring(1, indexIdentifier.Length - 2));
            }

            return GetCorrectObject<T>(remainingKey, row, (JObject)((JArray)usingResultSet[keyParts[0].Substring(0, keyParts[0].IndexOf('['))])[index]);
        }
    }
}
