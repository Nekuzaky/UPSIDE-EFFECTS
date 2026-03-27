using UnityEngine;

namespace Mindrift.Online.Auth
{
    public static class TokenStorage
    {
        private const string TokenKey = "Mindrift.Online.AuthToken";

        public static void SaveToken(string token)
        {
            string safeToken = string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim();
            PlayerPrefs.SetString(TokenKey, safeToken);
            PlayerPrefs.Save();
        }

        public static string LoadToken()
        {
            return PlayerPrefs.GetString(TokenKey, string.Empty).Trim();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.Save();
        }
    }
}
