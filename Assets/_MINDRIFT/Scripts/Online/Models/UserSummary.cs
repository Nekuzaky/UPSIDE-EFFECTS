using System;

namespace Mindrift.Online.Models
{
    [Serializable]
    public sealed class UserSummary
    {
        public string id;
        public string user_id;
        public string username;
        public string display_name;
        public string email;

        public string ResolveUserId()
        {
            if (!string.IsNullOrWhiteSpace(user_id))
            {
                return user_id.Trim();
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                return id.Trim();
            }

            return string.Empty;
        }

        public string ResolveDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(display_name))
            {
                return display_name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                return username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                return email.Trim();
            }

            return "PLAYER";
        }

        public void Sanitize()
        {
            id ??= string.Empty;
            user_id ??= string.Empty;
            username ??= string.Empty;
            display_name ??= string.Empty;
            email ??= string.Empty;
        }
    }
}
