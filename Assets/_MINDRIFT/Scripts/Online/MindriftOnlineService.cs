using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mindrift.Auth;
using Mindrift.Online.Auth;
using Mindrift.Online.Core;
using Mindrift.Online.Models;
using Mindrift.UI;
using UnityEngine;

namespace Mindrift.Online
{
    public sealed class MindriftOnlineService
    {
        [Serializable]
        private sealed class EmptyPayload
        {
        }

        private static MindriftOnlineService instance;

        private readonly ApiClient apiClient;
        private readonly AuthManager authManager;

        private bool suppressSettingsPush;
        private bool settingsSaveInProgress;
        private bool settingsSavePending;

        public static MindriftOnlineService Instance => instance ??= new MindriftOnlineService();

        public MindriftStatsDto CachedMyStats { get; private set; }
        public MindriftSettingsDto CachedSettings { get; private set; }
        public IReadOnlyList<LeaderboardEntryDto> CachedLeaderboard => cachedLeaderboard;

        private readonly List<LeaderboardEntryDto> cachedLeaderboard = new List<LeaderboardEntryDto>();

        private MindriftOnlineService()
        {
            apiClient = new ApiClient(ApiConfig.Active);
            authManager = AuthManager.Instance;
            authManager.SessionChanged += HandleAuthSessionChanged;
            SettingsManager.OnSettingsApplied += HandleLocalSettingsApplied;
        }

        public bool IsAuthenticated
        {
            get
            {
                AuthSessionData session = authManager.CurrentSession;
                return session != null && !session.isGuest && !string.IsNullOrWhiteSpace(session.authToken);
            }
        }

        public async Task RefreshAuthenticatedDataAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                return;
            }

            _ = await FetchMyStatsAsync(cancellationToken);
            _ = await FetchLeaderboardAsync(cancellationToken);
        }

        public async Task<ApiRequestResult<MindriftStatsDto>> FetchMyStatsAsync(CancellationToken cancellationToken = default)
        {
            if (!TryGetBearerToken(out string token))
            {
                return ApiRequestResult<MindriftStatsDto>.Unauthorized("Session expired. Please sign in again.");
            }

            ApiRequestResult<MindriftStatsDto> result = await apiClient.GetAsync<MindriftStatsDto>(
                ApiRoutes.MindriftMyStats,
                token,
                cancellationToken);

            if (result.Success && result.Data != null)
            {
                result.Data.Sanitize();
                CachedMyStats = result.Data;
            }
            else if (result.IsUnauthorized)
            {
                await authManager.SignOutAsync(cancellationToken);
            }

            return result;
        }

        public async Task<ApiRequestResult<LeaderboardResponseData>> FetchLeaderboardAsync(CancellationToken cancellationToken = default)
        {
            ApiRequestResult<LeaderboardResponseData> result = await apiClient.GetAsync<LeaderboardResponseData>(
                ApiRoutes.MindriftLeaderboard,
                bearerToken: null,
                cancellationToken);

            if (result.Success)
            {
                cachedLeaderboard.Clear();
                if (result.Data != null && result.Data.entries != null)
                {
                    for (int i = 0; i < result.Data.entries.Count; i++)
                    {
                        LeaderboardEntryDto entry = result.Data.entries[i];
                        if (entry == null)
                        {
                            continue;
                        }

                        entry.Sanitize();
                        cachedLeaderboard.Add(entry);
                    }
                }
            }

            return result;
        }

        public async Task<ApiRequestResult<MindriftSettingsDto>> FetchSettingsAsync(CancellationToken cancellationToken = default)
        {
            if (!TryGetBearerToken(out string token))
            {
                return ApiRequestResult<MindriftSettingsDto>.Unauthorized("Session expired. Please sign in again.");
            }

            ApiRequestResult<MindriftSettingsDto> result = await apiClient.GetAsync<MindriftSettingsDto>(
                ApiRoutes.MindriftSettingsGet,
                token,
                cancellationToken);

            if (result.Success && result.Data != null)
            {
                result.Data.Sanitize();
                CachedSettings = result.Data;
            }
            else if (result.IsUnauthorized)
            {
                await authManager.SignOutAsync(cancellationToken);
            }

            return result;
        }

        public async Task<ApiRequestResult<bool>> SaveSettingsAsync(MindriftSettingsDto settings, CancellationToken cancellationToken = default)
        {
            if (settings == null)
            {
                return ApiRequestResult<bool>.Failed("No settings payload provided.");
            }

            if (!TryGetBearerToken(out string token))
            {
                return ApiRequestResult<bool>.Unauthorized("Session expired. Please sign in again.");
            }

            settings.Sanitize();
            ApiRequestResult<EmptyPayload> result = await apiClient.PostAsync<MindriftSettingsDto, EmptyPayload>(
                ApiRoutes.MindriftSettingsSave,
                settings,
                token,
                cancellationToken);

            if (result.Success)
            {
                CachedSettings = settings;
                return ApiRequestResult<bool>.Succeeded(true, result.StatusCode, result.RawBody);
            }

            if (result.IsUnauthorized)
            {
                await authManager.SignOutAsync(cancellationToken);
            }

            return result.IsUnauthorized
                ? ApiRequestResult<bool>.Unauthorized(result.ErrorMessage, result.StatusCode, result.RawBody)
                : ApiRequestResult<bool>.Failed(result.ErrorMessage, result.StatusCode, result.RawBody);
        }

        public async Task<ApiRequestResult<bool>> SubmitRunAsync(MindriftRunSubmission runSubmission, CancellationToken cancellationToken = default)
        {
            if (runSubmission == null)
            {
                return ApiRequestResult<bool>.Failed("No run payload provided.");
            }

            if (!runSubmission.Validate(out string validationError))
            {
                return ApiRequestResult<bool>.Failed(validationError);
            }

            if (!TryGetBearerToken(out string token))
            {
                return ApiRequestResult<bool>.Unauthorized("Session expired. Please sign in again.");
            }

            ApiRequestResult<EmptyPayload> result = await apiClient.PostAsync<MindriftRunSubmission, EmptyPayload>(
                ApiRoutes.MindriftSaveRun,
                runSubmission,
                token,
                cancellationToken);

            if (result.Success)
            {
                return ApiRequestResult<bool>.Succeeded(true, result.StatusCode, result.RawBody);
            }

            if (result.IsUnauthorized)
            {
                await authManager.SignOutAsync(cancellationToken);
            }

            return result.IsUnauthorized
                ? ApiRequestResult<bool>.Unauthorized(result.ErrorMessage, result.StatusCode, result.RawBody)
                : ApiRequestResult<bool>.Failed(result.ErrorMessage, result.StatusCode, result.RawBody);
        }

        public async Task PullRemoteSettingsAndApplyAsync(CancellationToken cancellationToken = default)
        {
            ApiRequestResult<MindriftSettingsDto> result = await FetchSettingsAsync(cancellationToken);
            if (!result.Success || result.Data == null)
            {
                return;
            }

            suppressSettingsPush = true;
            try
            {
                result.Data.ApplyToLocalSettings();
            }
            finally
            {
                suppressSettingsPush = false;
            }
        }

        public void ClearCachedData()
        {
            CachedMyStats = null;
            CachedSettings = null;
            cachedLeaderboard.Clear();
        }

        private bool TryGetBearerToken(out string token)
        {
            token = authManager.CurrentSession != null ? authManager.CurrentSession.authToken : string.Empty;
            token = string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim();
            return !string.IsNullOrWhiteSpace(token);
        }

        private void HandleLocalSettingsApplied()
        {
            if (suppressSettingsPush || !IsAuthenticated)
            {
                return;
            }

            settingsSavePending = true;
            _ = FlushSettingsSaveQueueAsync();
        }

        private async Task FlushSettingsSaveQueueAsync()
        {
            if (settingsSaveInProgress)
            {
                return;
            }

            settingsSaveInProgress = true;
            try
            {
                while (settingsSavePending)
                {
                    settingsSavePending = false;
                    await Task.Delay(250);
                    if (!IsAuthenticated)
                    {
                        continue;
                    }

                    MindriftSettingsDto payload = MindriftSettingsDto.FromLocalSettings();
                    ApiRequestResult<bool> saveResult = await SaveSettingsAsync(payload);
                    if (!saveResult.Success)
                    {
                        LogWarning($"[MINDRIFT] Remote settings save failed: {saveResult.ErrorMessage}");
                    }
                }
            }
            finally
            {
                settingsSaveInProgress = false;
            }
        }

        private void HandleAuthSessionChanged(AuthSessionData session)
        {
            if (session == null || session.isGuest)
            {
                ClearCachedData();
                return;
            }

            _ = RefreshAuthenticatedDataAsync();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }
    }
}
