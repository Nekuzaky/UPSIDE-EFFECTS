using System;
using UnityEngine;

namespace Mindrift.Online.Models
{
    [Serializable]
    public sealed class LeaderboardEntryDto
    {
        public int rank;
        public string username;
        public string display_name;
        public int score;
        public float top_height;

        public void Sanitize()
        {
            rank = Mathf.Max(0, rank);
            username ??= string.Empty;
            display_name ??= string.Empty;
            score = Mathf.Max(0, score);
            top_height = Mathf.Max(0f, top_height);
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

            return "PLAYER";
        }
    }
}
