using System;
using UnityEngine;

namespace Mindrift.Online.Core
{
    public static class JsonHelper
    {
        public static string Serialize<T>(T payload, bool prettyPrint = false)
        {
            if (payload == null)
            {
                return "{}";
            }

            return JsonUtility.ToJson(payload, prettyPrint);
        }

        public static bool TryDeserialize<T>(string json, out T value, out string error)
        {
            value = default;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Empty response body.";
                return false;
            }

            try
            {
                value = JsonUtility.FromJson<T>(json);
                if (value == null)
                {
                    error = "JSON body could not be parsed.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"Malformed JSON response. {exception.Message}";
                return false;
            }
        }
    }
}
