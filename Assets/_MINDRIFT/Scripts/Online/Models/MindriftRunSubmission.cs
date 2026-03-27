using System;
using UnityEngine;

namespace Mindrift.Online.Models
{
    [Serializable]
    public sealed class MindriftRunSubmission
    {
        public int score;
        public float max_height;
        public float duration_seconds;
        public int deaths;
        public string build_version;

        public void Sanitize()
        {
            score = Mathf.Max(0, score);
            max_height = Mathf.Max(0f, max_height);
            duration_seconds = Mathf.Max(0f, duration_seconds);
            deaths = Mathf.Max(0, deaths);
            build_version = string.IsNullOrWhiteSpace(build_version) ? Application.version : build_version.Trim();
        }

        public bool Validate(out string errorMessage)
        {
            Sanitize();
            if (duration_seconds <= 0f)
            {
                errorMessage = "Run duration must be greater than zero.";
                return false;
            }

            if (score <= 0 && max_height <= 0f)
            {
                errorMessage = "Run has no score or height progression.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
