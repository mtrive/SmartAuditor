using System;
using System.IO;
using Newtonsoft.Json;

namespace SmartAuditor.Editor.Utils
{
    internal static class Json
    {
        const string k_DateFormatString = "yyyy-MM-ddTHH:mm:ssZ";

        static readonly JsonSerializerSettings k_JsonSerializerSettings = new JsonSerializerSettings
        {
            DateFormatString = k_DateFormatString
        };

        /// <summary>
        /// Serializes a DateTime object to a JSON-formatted string using a UTC format.
        /// </summary>
        /// <param name="dateTime">The DateTime object to serialize.</param>
        /// <returns>A JSON-formatted string representing the DateTime object in UTC.</returns>
        public static string SerializeDateTime(DateTime dateTime)
        {
            return JsonConvert.SerializeObject(dateTime, k_JsonSerializerSettings);
        }

        /// <summary>
        /// Deserializes a JSON-formatted string representing a DateTime in UTC to a DateTime object.
        /// </summary>
        /// <param name="utcDateTime">The JSON-formatted string representing the DateTime object in UTC.</param>
        /// <returns>A DateTime object converted to local time.</returns>
        public static DateTime DeserializeDateTime(string utcDateTime)
        {
            return JsonConvert.DeserializeObject<DateTime>(utcDateTime, k_JsonSerializerSettings).ToLocalTime();
        }

        public static T[] DeserializeArray<T>(string json)
        {
            var array = JsonConvert.DeserializeObject<T[]>(json, k_JsonSerializerSettings);
            return array;
        }

        public static T[] DeserializeArrayFromFile<T>(string fileName)
        {
            var fullPath = Path.GetFullPath(fileName);
            var json = File.ReadAllText(fullPath);
            var items = Json.DeserializeArray<T>(json);

            return items;
        }

        public static string SerializeArray<T>(T[] array)
        {
            return JsonConvert.SerializeObject(array, k_JsonSerializerSettings);
        }

        public static string SerializeArray<T>(T[] array, bool prettyPrint)
        {
            return JsonConvert.SerializeObject(array, prettyPrint ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None, k_JsonSerializerSettings);
        }
    }
}
