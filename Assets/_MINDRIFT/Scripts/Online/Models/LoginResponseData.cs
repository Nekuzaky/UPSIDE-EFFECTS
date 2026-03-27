using System;

namespace Mindrift.Online.Models
{
    [Serializable]
    public sealed class LoginResponseData
    {
        public string token;
        public string access_token;
        public string auth_token;
        public UserSummary user;

        public string ResolveToken()
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token.Trim();
            }

            if (!string.IsNullOrWhiteSpace(access_token))
            {
                return access_token.Trim();
            }

            if (!string.IsNullOrWhiteSpace(auth_token))
            {
                return auth_token.Trim();
            }

            return string.Empty;
        }
    }
}
