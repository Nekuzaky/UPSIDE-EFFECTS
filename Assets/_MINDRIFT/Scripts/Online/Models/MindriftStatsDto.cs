using System;
using Mindrift.Core;
using UnityEngine;

namespace Mindrift.Online.Models
{
    [Serializable]
    public sealed class MindriftStatsDto
    {
        public int total_runs;
        public int total_deaths;
        public int top_score;
        public float top_height;
        public float total_playtime_seconds;
        public string last_updated_utc;

        public void Sanitize()
        {
            total_runs = Mathf.Max(0, total_runs);
            total_deaths = Mathf.Max(0, total_deaths);
            top_score = Mathf.Max(0, top_score);
            top_height = Mathf.Max(0f, top_height);
            total_playtime_seconds = Mathf.Max(0f, total_playtime_seconds);
            last_updated_utc ??= string.Empty;
        }

        public PlayerStatsData ToLocalStatsData()
        {
            Sanitize();
            return new PlayerStatsData
            {
                totalRuns = total_runs,
                totalDeaths = total_deaths,
                topScore = top_score,
                topHeight = top_height,
                lastUpdatedUtc = string.IsNullOrWhiteSpace(last_updated_utc) ? string.Empty : last_updated_utc
            };
        }
    }
}
