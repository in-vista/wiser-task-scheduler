using System;
using System.Collections.Generic;
using GeeksCoreLibrary.Core.Extensions;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Core.Helpers
{
    public static class ReplacementHelper
    {
        /// <summary>
        /// Prepare the given string for usage.
        /// The final string is returned in Item 1.
        /// Replaces parameter placeholders with an easy accessible name and stores that name in a list that is returned in Item 2.
        /// If a parameter is a collection it is replaced by a comma separated value directly.
        /// </summary>
        /// <param name="originalString">The string to prepare.</param>
        /// <param name="usingResultSet">The result set that is used.</param>
        /// <param name="remainingKey">The remainder of they key (after the first .) to be used for collections.</param>
        /// <param name="mySqlSafe">If the values from the result set needs to be safe for MySQL.</param>
        /// <param name="htmlEncode">If the values from the result set needs to be HTML encoded.</param>
        /// <returns></returns>
        public static Tuple<string, List<string>> PrepareText(string originalString, JObject usingResultSet, string remainingKey, bool mySqlSafe = false, bool htmlEncode = false)
        {
            var result = originalString;
            var parameterKeys = new List<string>();

            while (result.Contains("[{") && result.Contains("}]"))
            {
                var startIndex = result.IndexOf("[{") + 2;
                var endIndex = result.IndexOf("}]");

                var key = result.Substring(startIndex, endIndex - startIndex);

                if (key.Contains("[]"))
                {
                    key = key.Replace("[]", "");

                    var values = new List<string>();
                    var lastKeyIndex = key.LastIndexOf('.');

                    var usingResultSetArray = ResultSetHelper.GetCorrectObject<JArray>($"{(remainingKey.Length > 0 ? $"{remainingKey}." : "")}{key.Substring(0, lastKeyIndex)}", 0, usingResultSet);
                    for (var i = 0; i < usingResultSetArray.Count; i++)
                    {
                        values.Add(GetValue(key.Substring(lastKeyIndex + 1), i, (JObject)usingResultSetArray[i], mySqlSafe, htmlEncode));
                    }

                    result = result.Replace($"[{{{key}[]}}]", String.Join(",", values));
                }
                else if (key.Contains("<>"))
                {
                    key = key.Replace("<>", "");
                    result = result.Replace($"[{{{key}<>}}]", GetValue(key, 0, ResultSetHelper.GetCorrectObject<JObject>(remainingKey, 0, usingResultSet), mySqlSafe, htmlEncode));
                }
                else
                {
                    result = result.Replace($"[{{{key}}}]", $"?{key}");
                    parameterKeys.Add(key);
                }
            }

            return new Tuple<string, List<string>>(result, parameterKeys);
        }

        /// <summary>
        /// Replaces the parameters in the given string with the corresponding values of the requested row.
        /// </summary>
        /// <param name="originalString">The string that needs replacements.</param>
        /// <param name="row">The row to use the values from.</param>
        /// <param name="parameterKeys">The keys of the parameters to replace.</param>
        /// <param name="usingResultSet">The result set that is used.</param>
        /// <param name="mySqlSafe">If the values from the result set needs to be safe for MySQL.</param>
        /// <param name="htmlEncode">If the values from the result set needs to be HTML encoded.</param>
        /// <returns></returns>
        public static string ReplaceText(string originalString, int row, List<string> parameterKeys, JObject usingResultSet, bool mySqlSafe = false, bool htmlEncode = false)
        {
            var result = originalString;
            
            foreach (var key in parameterKeys)
            {
                result = result.Replace($"?{key}", GetValue(key, row, usingResultSet, mySqlSafe, htmlEncode));
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key">The key to get the value from.</param>
        /// <param name="row">The row to get the value from.</param>
        /// <param name="usingResultSet">The result set that is used.</param>
        /// <param name="mySqlSafe">If the value from the result set needs to be safe for MySQL.</param>
        /// <param name="htmlEncode">If the value from the result set needs to be HTML encoded.</param>
        /// <returns></returns>
        private static string GetValue(string key, int row, JObject usingResultSet, bool mySqlSafe, bool htmlEncode)
        {
            var value = (string)ResultSetHelper.GetCorrectObject<JValue>(key, row, usingResultSet);

            if (mySqlSafe)
            {
                value = value.ToMySqlSafeValue();
            }

            if (htmlEncode)
            {
                value = value.HtmlEncode();
            }

            return value;
        }
    }
}
