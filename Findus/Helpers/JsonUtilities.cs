using System;
using System.IO;
using Newtonsoft.Json;

namespace Findus.Helpers
{
    public static class JsonUtilities
    {
        public static T LoadJson<T>(string jsonFilePath, JsonSerializerSettings jsonSettings = null!)
        {
            using StreamReader r = new StreamReader(jsonFilePath);
            string json = r.ReadToEnd();
            if (string.IsNullOrEmpty(json)) throw new ArgumentException($"Failed to parse json file {jsonFilePath}");
            return JsonConvert.DeserializeObject<T>(json, jsonSettings);
        }
    }
}