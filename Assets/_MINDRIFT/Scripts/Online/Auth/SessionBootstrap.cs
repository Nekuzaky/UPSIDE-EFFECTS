using Mindrift.Auth;
using Mindrift.Online.Core;
using UnityEngine;

namespace Mindrift.Online.Auth
{
    public static class SessionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!ApiConfig.Active.UseOnlineAuth)
            {
                return;
            }

            AuthRuntime.Override(AuthManager.Instance);
            _ = AuthManager.Instance.TryRestoreSessionAsync();
            _ = MindriftOnlineService.Instance;
        }
    }
}
