using System;
using System.Threading;
using System.Threading.Tasks;
using Mindrift.Auth;
using Mindrift.Online.Core;
using Mindrift.Online.Models;
using UnityEngine;

namespace Mindrift.Online.Auth
{
    public sealed class AuthManager : IAuthService
    {
        private static AuthManager instance;

        private readonly ApiClient apiClient;
        private AuthSessionData currentSession;
        private UserSummary currentUser;
        private Task<AuthSessionData> restoreSessionTask;
        private bool hasAttemptedRestore;

        public static AuthManager Instance => instance ??= new AuthManager();

        public event Action<AuthSessionData> SessionChanged;

        public AuthSessionData CurrentSession => currentSession ??= AuthSessionData.CreateGuest();
        public UserSummary CurrentUser => currentUser;

        private AuthManager()
        {
            apiClient = new ApiClient(ApiConfig.Active);
            currentSession = AuthSessionData.CreateGuest();
        }

        public AuthSessionData TryRestoreSession()
        {
            if (!hasAttemptedRestore)
            {
                _ = TryRestoreSessionAsync();
            }

            return CurrentSession;
        }

        public AuthOperationResult Register(string username, string email, string password)
        {
            return RegisterAsync(username, email, password).GetAwaiter().GetResult();
        }

        public AuthOperationResult SignIn(string identifier, string password)
        {
            return SignInAsync(identifier, password).GetAwaiter().GetResult();
        }

        public void SignOut()
        {
            SignOutAsync().GetAwaiter().GetResult();
        }

        public Task<AuthSessionData> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            if (restoreSessionTask != null && !restoreSessionTask.IsCompleted)
            {
                return restoreSessionTask;
            }

            restoreSessionTask = RestoreSessionInternalAsync(cancellationToken);
            return restoreSessionTask;
        }

        public async Task<AuthOperationResult> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return AuthOperationResult.Failed("Registration is managed on nekuzaky.com. Create your account on the website, then sign in here.");
        }

        public async Task<AuthOperationResult> SignInAsync(string identifier, string password, CancellationToken cancellationToken = default)
        {
            string safeIdentifier = string.IsNullOrWhiteSpace(identifier) ? string.Empty : identifier.Trim();
            string safePassword = string.IsNullOrWhiteSpace(password) ? string.Empty : password.Trim();

            if (string.IsNullOrWhiteSpace(safeIdentifier) || string.IsNullOrWhiteSpace(safePassword))
            {
                return AuthOperationResult.Failed("Enter username/email and password.");
            }

            LoginRequest request = new LoginRequest
            {
                identifier = safeIdentifier,
                password = safePassword
            };

            ApiRequestResult<LoginResponseData> loginResult = await apiClient.PostAsync<LoginRequest, LoginResponseData>(
                ApiRoutes.AuthLogin,
                request,
                bearerToken: null,
                cancellationToken);

            if (!loginResult.Success)
            {
                if (loginResult.IsUnauthorized)
                {
                    return AuthOperationResult.Failed("Invalid credentials.");
                }

                return AuthOperationResult.Failed(string.IsNullOrWhiteSpace(loginResult.ErrorMessage)
                    ? "Unable to connect to server."
                    : loginResult.ErrorMessage);
            }

            LoginResponseData responseData = loginResult.Data;
            string token = responseData != null ? responseData.ResolveToken() : string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                return AuthOperationResult.Failed("Login succeeded but token is missing.");
            }

            UserSummary user = responseData != null ? responseData.user : null;
            if (user == null)
            {
                ApiRequestResult<UserSummary> meResult = await apiClient.GetAsync<UserSummary>(ApiRoutes.AuthMe, token, cancellationToken);
                if (meResult.Success)
                {
                    user = meResult.Data;
                }
            }

            user ??= BuildFallbackUser(safeIdentifier);
            SetAuthenticatedSession(token, user);

            _ = MindriftOnlineService.Instance.PullRemoteSettingsAndApplyAsync(cancellationToken);
            return AuthOperationResult.Succeeded(CurrentSession, $"Welcome, {CurrentSession.DisplayName}.");
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            TokenStorage.Clear();
            SetGuestSession();
            return Task.CompletedTask;
        }

        private async Task<AuthSessionData> RestoreSessionInternalAsync(CancellationToken cancellationToken)
        {
            hasAttemptedRestore = true;
            string token = TokenStorage.LoadToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                SetGuestSession();
                return CurrentSession;
            }

            ApiRequestResult<UserSummary> meResult = await apiClient.GetAsync<UserSummary>(
                ApiRoutes.AuthMe,
                token,
                cancellationToken);

            if (meResult.Success && meResult.Data != null)
            {
                SetAuthenticatedSession(token, meResult.Data);
                _ = MindriftOnlineService.Instance.PullRemoteSettingsAndApplyAsync(cancellationToken);
                return CurrentSession;
            }

            if (meResult.IsUnauthorized)
            {
                TokenStorage.Clear();
            }

            SetGuestSession();
            return CurrentSession;
        }

        private void SetAuthenticatedSession(string token, UserSummary user)
        {
            user ??= new UserSummary();
            user.Sanitize();
            string userId = user.ResolveUserId();
            string username = user.ResolveDisplayName();

            AuthSessionData session = new AuthSessionData
            {
                userId = string.IsNullOrWhiteSpace(userId) ? username.ToLowerInvariant() : userId,
                username = username,
                email = user.email ?? string.Empty,
                authToken = string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim(),
                signedInAtUtc = DateTime.UtcNow.ToString("O"),
                provider = "nekuzaky_api",
                isGuest = false
            };
            session.Sanitize();

            currentUser = user;
            currentSession = session;
            TokenStorage.SaveToken(session.authToken);
            SessionChanged?.Invoke(currentSession);
        }

        private void SetGuestSession()
        {
            currentUser = null;
            currentSession = AuthSessionData.CreateGuest();
            SessionChanged?.Invoke(currentSession);
        }

        private static UserSummary BuildFallbackUser(string identifier)
        {
            string trimmed = string.IsNullOrWhiteSpace(identifier) ? "PLAYER" : identifier.Trim();
            return new UserSummary
            {
                id = string.Empty,
                user_id = string.Empty,
                username = trimmed,
                display_name = trimmed,
                email = trimmed.Contains("@", StringComparison.Ordinal) ? trimmed : string.Empty
            };
        }
    }
}
