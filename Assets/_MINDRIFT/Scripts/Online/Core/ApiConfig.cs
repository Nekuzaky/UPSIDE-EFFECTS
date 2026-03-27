using UnityEngine;

namespace Mindrift.Online.Core
{
    [CreateAssetMenu(fileName = "MindriftApiConfig", menuName = "MINDRIFT/Online/API Config")]
    public sealed class ApiConfig : ScriptableObject
    {
        [Header("Connection")]
        [SerializeField] private string baseApiUrl = "https://nekuzaky.com/api";
        [SerializeField] [Min(5)] private int timeoutSeconds = 20;

        [Header("Behavior")]
        [SerializeField] private bool useOnlineAuth = true;
        [SerializeField] private bool verboseLogging;

        private static ApiConfig cached;

        public string BaseApiUrl => NormalizeBaseUrl(baseApiUrl);
        public int TimeoutSeconds => Mathf.Clamp(timeoutSeconds, 5, 120);
        public bool UseOnlineAuth => useOnlineAuth;
        public bool VerboseLogging => verboseLogging;

        public static ApiConfig Active => cached != null ? cached : (cached = LoadConfig());

        public static void Reload()
        {
            cached = LoadConfig();
        }

        private static ApiConfig LoadConfig()
        {
            ApiConfig loaded = Resources.Load<ApiConfig>("MindriftApiConfig");
            if (loaded != null)
            {
                return loaded;
            }

            ApiConfig runtimeFallback = CreateInstance<ApiConfig>();
            runtimeFallback.name = "RuntimeApiConfig";
            return runtimeFallback;
        }

        private static string NormalizeBaseUrl(string rawValue)
        {
            string safe = string.IsNullOrWhiteSpace(rawValue)
                ? "https://nekuzaky.com/api"
                : rawValue.Trim();

            while (safe.EndsWith("/"))
            {
                safe = safe.Substring(0, safe.Length - 1);
            }

            return safe;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            timeoutSeconds = Mathf.Clamp(timeoutSeconds, 5, 120);
        }
#endif
    }
}
