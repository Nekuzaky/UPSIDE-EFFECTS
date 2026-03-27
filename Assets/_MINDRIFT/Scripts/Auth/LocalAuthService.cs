using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Mindrift.Auth
{
    public sealed class LocalAuthService : IAuthService
    {
        [Serializable]
        private sealed class AuthAccountRecord
        {
            public AuthUserProfile profile = new AuthUserProfile();
            public string passwordHash;
            public string passwordSalt;
            public string lastLoginUtc;

            public void Sanitize()
            {
                profile ??= new AuthUserProfile();
                profile.Sanitize();
                passwordHash ??= string.Empty;
                passwordSalt ??= string.Empty;
                lastLoginUtc ??= string.Empty;
            }
        }

        [Serializable]
        private sealed class AuthAccountsStore
        {
            public List<AuthAccountRecord> accounts = new List<AuthAccountRecord>();

            public void Sanitize()
            {
                accounts ??= new List<AuthAccountRecord>();
                for (int i = accounts.Count - 1; i >= 0; i--)
                {
                    if (accounts[i] == null)
                    {
                        accounts.RemoveAt(i);
                        continue;
                    }

                    accounts[i].Sanitize();
                }
            }
        }

        [Serializable]
        private sealed class AuthSessionStore
        {
            public AuthSessionData session = AuthSessionData.CreateGuest();

            public void Sanitize()
            {
                session ??= AuthSessionData.CreateGuest();
                session.Sanitize();
            }
        }

        private const string AccountsFileName = "mindrift_auth_accounts.json";
        private const string SessionFileName = "mindrift_auth_session.json";

        private AuthAccountsStore cachedAccounts;
        private AuthSessionData currentSession;

        public event Action<AuthSessionData> SessionChanged;

        public AuthSessionData CurrentSession
        {
            get
            {
                if (currentSession == null)
                {
                    TryRestoreSession();
                }

                return currentSession;
            }
        }

        private static string AccountsPath => Path.Combine(Application.persistentDataPath, AccountsFileName);
        private static string SessionPath => Path.Combine(Application.persistentDataPath, SessionFileName);

        public AuthSessionData TryRestoreSession()
        {
            cachedAccounts = LoadAccounts();

            AuthSessionStore sessionStore = LoadSessionStore();
            AuthSessionData restoredSession = ValidateSession(sessionStore.session, cachedAccounts);
            SetCurrentSession(restoredSession, saveToDisk: false);
            return currentSession;
        }

        public AuthOperationResult Register(string username, string email, string password)
        {
            cachedAccounts = LoadAccounts();

            string normalizedUsername = NormalizeUsername(username);
            string normalizedEmail = NormalizeEmail(email);
            password = password?.Trim() ?? string.Empty;

            if (normalizedUsername.Length < 3)
            {
                return AuthOperationResult.Failed("Username must contain at least 3 characters.");
            }

            if (!IsValidEmail(normalizedEmail))
            {
                return AuthOperationResult.Failed("Enter a valid email address.");
            }

            if (password.Length < 6)
            {
                return AuthOperationResult.Failed("Password must contain at least 6 characters.");
            }

            for (int i = 0; i < cachedAccounts.accounts.Count; i++)
            {
                AuthAccountRecord existing = cachedAccounts.accounts[i];
                if (string.Equals(existing.profile.username, normalizedUsername, StringComparison.OrdinalIgnoreCase))
                {
                    return AuthOperationResult.Failed("This username is already taken.");
                }

                if (string.Equals(existing.profile.email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return AuthOperationResult.Failed("This email is already registered.");
                }
            }

            string salt = CreateSalt();
            string now = DateTime.UtcNow.ToString("O");
            AuthAccountRecord created = new AuthAccountRecord
            {
                profile = new AuthUserProfile
                {
                    userId = Guid.NewGuid().ToString("N"),
                    username = normalizedUsername,
                    email = normalizedEmail,
                    createdAtUtc = now
                },
                passwordSalt = salt,
                passwordHash = HashPassword(password, salt),
                lastLoginUtc = now
            };

            created.Sanitize();
            cachedAccounts.accounts.Add(created);
            SaveAccounts(cachedAccounts);

            AuthSessionData session = CreateSession(created.profile);
            SetCurrentSession(session, saveToDisk: true);
            return AuthOperationResult.Succeeded(session, "Account created. Local session is ready for API sync.");
        }

        public AuthOperationResult SignIn(string identifier, string password)
        {
            cachedAccounts = LoadAccounts();

            string normalizedIdentifier = (identifier ?? string.Empty).Trim();
            password = password?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedIdentifier))
            {
                return AuthOperationResult.Failed("Enter your username or email.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return AuthOperationResult.Failed("Enter your password.");
            }

            AuthAccountRecord account = FindAccount(cachedAccounts, normalizedIdentifier);
            if (account == null)
            {
                return AuthOperationResult.Failed("No account matches these credentials.");
            }

            string computedHash = HashPassword(password, account.passwordSalt);
            if (!string.Equals(computedHash, account.passwordHash, StringComparison.Ordinal))
            {
                return AuthOperationResult.Failed("Incorrect password.");
            }

            account.lastLoginUtc = DateTime.UtcNow.ToString("O");
            SaveAccounts(cachedAccounts);

            AuthSessionData session = CreateSession(account.profile);
            SetCurrentSession(session, saveToDisk: true);
            return AuthOperationResult.Succeeded(session, $"Welcome back, {session.DisplayName}.");
        }

        public void SignOut()
        {
            SetCurrentSession(AuthSessionData.CreateGuest(), saveToDisk: true);
        }

        public Task<AuthSessionData> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TryRestoreSession());
        }

        public Task<AuthOperationResult> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Register(username, email, password));
        }

        public Task<AuthOperationResult> SignInAsync(string identifier, string password, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SignIn(identifier, password));
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            SignOut();
            return Task.CompletedTask;
        }

        private void SetCurrentSession(AuthSessionData session, bool saveToDisk)
        {
            session ??= AuthSessionData.CreateGuest();
            session.Sanitize();

            currentSession = session;
            if (saveToDisk)
            {
                SaveSessionStore(new AuthSessionStore { session = currentSession });
            }

            SessionChanged?.Invoke(currentSession);
        }

        private static AuthSessionData ValidateSession(AuthSessionData session, AuthAccountsStore accountsStore)
        {
            session ??= AuthSessionData.CreateGuest();
            session.Sanitize();

            if (session.isGuest)
            {
                return AuthSessionData.CreateGuest();
            }

            AuthAccountRecord account = FindAccountById(accountsStore, session.userId);
            if (account == null)
            {
                return AuthSessionData.CreateGuest();
            }

            return CreateSession(account.profile);
        }

        private static AuthSessionData CreateSession(AuthUserProfile profile)
        {
            profile ??= new AuthUserProfile();
            profile.Sanitize();

            AuthSessionData session = new AuthSessionData
            {
                userId = profile.userId,
                username = profile.username,
                email = profile.email,
                authToken = Guid.NewGuid().ToString("N"),
                signedInAtUtc = DateTime.UtcNow.ToString("O"),
                provider = "local",
                isGuest = false
            };

            session.Sanitize();
            return session;
        }

        private static AuthAccountRecord FindAccount(AuthAccountsStore accountsStore, string identifier)
        {
            if (accountsStore == null || string.IsNullOrWhiteSpace(identifier))
            {
                return null;
            }

            string normalizedIdentifier = identifier.Trim();
            for (int i = 0; i < accountsStore.accounts.Count; i++)
            {
                AuthAccountRecord account = accountsStore.accounts[i];
                if (account == null)
                {
                    continue;
                }

                if (string.Equals(account.profile.username, normalizedIdentifier, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(account.profile.email, normalizedIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    return account;
                }
            }

            return null;
        }

        private static AuthAccountRecord FindAccountById(AuthAccountsStore accountsStore, string userId)
        {
            if (accountsStore == null || string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            for (int i = 0; i < accountsStore.accounts.Count; i++)
            {
                AuthAccountRecord account = accountsStore.accounts[i];
                if (account != null && string.Equals(account.profile.userId, userId, StringComparison.OrdinalIgnoreCase))
                {
                    return account;
                }
            }

            return null;
        }

        private static AuthAccountsStore LoadAccounts()
        {
            try
            {
                if (!File.Exists(AccountsPath))
                {
                    return new AuthAccountsStore();
                }

                string json = File.ReadAllText(AccountsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new AuthAccountsStore();
                }

                AuthAccountsStore loaded = JsonUtility.FromJson<AuthAccountsStore>(json) ?? new AuthAccountsStore();
                loaded.Sanitize();
                return loaded;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MINDRIFT] Failed to load local auth accounts from '{AccountsPath}'. {exception.Message}");
                return new AuthAccountsStore();
            }
        }

        private static AuthSessionStore LoadSessionStore()
        {
            try
            {
                if (!File.Exists(SessionPath))
                {
                    return new AuthSessionStore();
                }

                string json = File.ReadAllText(SessionPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new AuthSessionStore();
                }

                AuthSessionStore loaded = JsonUtility.FromJson<AuthSessionStore>(json) ?? new AuthSessionStore();
                loaded.Sanitize();
                return loaded;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MINDRIFT] Failed to load auth session from '{SessionPath}'. {exception.Message}");
                return new AuthSessionStore();
            }
        }

        private static void SaveAccounts(AuthAccountsStore accountsStore)
        {
            accountsStore ??= new AuthAccountsStore();
            accountsStore.Sanitize();
            SaveJson(AccountsPath, JsonUtility.ToJson(accountsStore, true));
        }

        private static void SaveSessionStore(AuthSessionStore sessionStore)
        {
            sessionStore ??= new AuthSessionStore();
            sessionStore.Sanitize();
            SaveJson(SessionPath, JsonUtility.ToJson(sessionStore, true));
        }

        private static void SaveJson(string path, string json)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json ?? string.Empty);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MINDRIFT] Failed to save auth data to '{path}'. {exception.Message}");
            }
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            int atIndex = email.IndexOf('@');
            int dotIndex = email.LastIndexOf('.');
            return atIndex > 0 && dotIndex > atIndex + 1 && dotIndex < email.Length - 1;
        }

        private static string NormalizeUsername(string username)
        {
            return (username ?? string.Empty).Trim();
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string CreateSalt()
        {
            byte[] bytes = new byte[16];
            using RandomNumberGenerator random = RandomNumberGenerator.Create();
            random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string HashPassword(string password, string salt)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes($"{salt}:{password}");
            using SHA256 sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(inputBytes));
        }
    }
}
