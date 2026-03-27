using System;
using System.Collections.Generic;

namespace Mindrift.Online.Models
{
    [Serializable]
    public sealed class LeaderboardResponseData
    {
        public List<LeaderboardEntryDto> entries = new List<LeaderboardEntryDto>();
    }
}
