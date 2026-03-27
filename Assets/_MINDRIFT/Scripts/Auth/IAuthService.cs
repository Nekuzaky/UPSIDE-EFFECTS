using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mindrift.Auth
{
    public interface IAuthService
    {
        AuthSessionData CurrentSession { get; }
        event Action<AuthSessionData> SessionChanged;

        AuthSessionData TryRestoreSession();
        AuthOperationResult Register(string username, string email, string password);
        AuthOperationResult SignIn(string identifier, string password);
        void SignOut();

        Task<AuthSessionData> TryRestoreSessionAsync(CancellationToken cancellationToken = default);
        Task<AuthOperationResult> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken = default);
        Task<AuthOperationResult> SignInAsync(string identifier, string password, CancellationToken cancellationToken = default);
        Task SignOutAsync(CancellationToken cancellationToken = default);
    }
}
