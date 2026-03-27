using System.Threading.Tasks;
using Mindrift.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace Mindrift.Online.UI
{
    public sealed class LoginPanelController : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private InputField identifierInput;
        [SerializeField] private InputField passwordInput;

        [Header("Buttons")]
        [SerializeField] private Button signInButton;
        [SerializeField] private Button signOutButton;

        [Header("Feedback")]
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject loadingIndicator;

        private IAuthService authService;
        private bool isBusy;

        private void Awake()
        {
            authService = AuthRuntime.Service;
            WireEvents();
            RefreshState();
        }

        private void OnEnable()
        {
            authService ??= AuthRuntime.Service;
            authService.SessionChanged += HandleSessionChanged;
            RefreshState();
        }

        private void OnDisable()
        {
            if (authService != null)
            {
                authService.SessionChanged -= HandleSessionChanged;
            }

            UnwireEvents();
        }

        private void WireEvents()
        {
            if (signInButton != null)
            {
                signInButton.onClick.RemoveListener(HandleSignInClicked);
                signInButton.onClick.AddListener(HandleSignInClicked);
            }

            if (signOutButton != null)
            {
                signOutButton.onClick.RemoveListener(HandleSignOutClicked);
                signOutButton.onClick.AddListener(HandleSignOutClicked);
            }
        }

        private void UnwireEvents()
        {
            if (signInButton != null)
            {
                signInButton.onClick.RemoveListener(HandleSignInClicked);
            }

            if (signOutButton != null)
            {
                signOutButton.onClick.RemoveListener(HandleSignOutClicked);
            }
        }

        private async void HandleSignInClicked()
        {
            if (isBusy || authService == null)
            {
                return;
            }

            SetBusy(true, "Signing in...");
            try
            {
                string identifier = identifierInput != null ? identifierInput.text : string.Empty;
                string password = passwordInput != null ? passwordInput.text : string.Empty;
                AuthOperationResult result = await authService.SignInAsync(identifier, password);
                if (result != null)
                {
                    SetStatus(result.message);
                }
            }
            finally
            {
                SetBusy(false);
                RefreshState();
            }
        }

        private async void HandleSignOutClicked()
        {
            if (isBusy || authService == null)
            {
                return;
            }

            SetBusy(true, "Signing out...");
            try
            {
                await authService.SignOutAsync();
            }
            finally
            {
                SetBusy(false);
                RefreshState();
            }
        }

        private void HandleSessionChanged(AuthSessionData session)
        {
            RefreshState();
            if (session == null || session.isGuest)
            {
                SetStatus("Signed out.");
            }
            else
            {
                SetStatus($"Signed in as {session.DisplayName}.");
            }
        }

        private void RefreshState()
        {
            bool isGuest = authService == null || authService.CurrentSession == null || authService.CurrentSession.isGuest;

            if (identifierInput != null)
            {
                identifierInput.interactable = !isBusy && isGuest;
            }

            if (passwordInput != null)
            {
                passwordInput.interactable = !isBusy && isGuest;
            }

            if (signInButton != null)
            {
                signInButton.gameObject.SetActive(isGuest);
                signInButton.interactable = !isBusy;
            }

            if (signOutButton != null)
            {
                signOutButton.gameObject.SetActive(!isGuest);
                signOutButton.interactable = !isBusy;
            }
        }

        private void SetBusy(bool value, string status = null)
        {
            isBusy = value;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                SetStatus(status);
            }
        }

        private void SetStatus(string message)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }
    }
}
