using System.Collections.Generic;
using System.Threading.Tasks;
using Mindrift.Online.Core;
using Mindrift.Online.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Mindrift.Online.UI
{
    public sealed class LeaderboardPanelController : MonoBehaviour
    {
        [SerializeField] private Text statusText;
        [SerializeField] private List<Text> rankNameTexts = new List<Text>();
        [SerializeField] private List<Text> scoreTexts = new List<Text>();

        private void OnEnable()
        {
            _ = ReloadAsync();
        }

        public async Task ReloadAsync()
        {
            SetStatus("Loading leaderboard...");
            ApiRequestResult<LeaderboardResponseData> result = await MindriftOnlineService.Instance.FetchLeaderboardAsync();

            if (!result.Success || result.Data == null)
            {
                SetStatus("Failed to load leaderboard.");
                return;
            }

            List<LeaderboardEntryDto> entries = result.Data.entries ?? new List<LeaderboardEntryDto>();
            int rowCount = Mathf.Min(rankNameTexts.Count, scoreTexts.Count);
            for (int i = 0; i < rowCount; i++)
            {
                Text nameText = rankNameTexts[i];
                Text scoreText = scoreTexts[i];

                if (i < entries.Count && entries[i] != null)
                {
                    LeaderboardEntryDto entry = entries[i];
                    entry.Sanitize();
                    if (nameText != null) nameText.text = $"{i + 1}. {entry.ResolveDisplayName()}";
                    if (scoreText != null) scoreText.text = entry.score.ToString();
                }
                else
                {
                    if (nameText != null) nameText.text = $"{i + 1}. ---";
                    if (scoreText != null) scoreText.text = "0";
                }
            }

            SetStatus(entries.Count == 0 ? "Leaderboard is empty." : string.Empty);
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value ?? string.Empty;
            }
        }
    }
}
