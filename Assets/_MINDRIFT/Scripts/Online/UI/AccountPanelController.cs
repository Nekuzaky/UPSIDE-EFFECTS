using Mindrift.Auth;
using Mindrift.Online.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Mindrift.Online.UI
{
    public sealed class AccountPanelController : MonoBehaviour
    {
        [SerializeField] private Text accountStateText;
        [SerializeField] private Text displayNameText;
        [SerializeField] private Text emailText;
        [SerializeField] private Text bestScoreText;
        [SerializeField] private Text bestHeightText;
        [SerializeField] private Button signOutButton;

        private IAuthService authService;

        private void Awake()
        {
            authService = AuthRuntime.Service;
            if (signOutButton != null)
            {
                signOutButton.onClick.RemoveListener(HandleSignOutClicked);
                signOutButton.onClick.AddListener(HandleSignOutClicked);
            }

            RefreshView();
        }

        private void OnEnable()
        {
            authService ??= AuthRuntime.Service;
            authService.SessionChanged += HandleSessionChanged;
            RefreshView();
        }

        private void OnDisable()
        {
            if (authService != null)
            {
                authService.SessionChanged -= HandleSessionChanged;
            }

            if (signOutButton != null)
            {
                signOutButton.onClick.RemoveListener(HandleSignOutClicked);
            }
        }

        public void RefreshStats(MindriftStatsDto stats)
        {
            if (stats == null)
            {
                if (bestScoreText != null) bestScoreText.text = "0";
                if (bestHeightText != null) bestHeightText.text = "0.00";
                return;
            }

            stats.Sanitize();
            if (bestScoreText != null) bestScoreText.text = stats.top_score.ToString();
            if (bestHeightText != null) bestHeightText.text = stats.top_height.ToString("0.00");
        }

        private async void HandleSignOutClicked()
        {
            if (authService == null)
            {
                return;
            }

            await authService.SignOutAsync();
        }

        private void HandleSessionChanged(AuthSessionData _)
        {
            RefreshView();
        }

        private void RefreshView()
        {
            AuthSessionData session = authService != null ? authService.CurrentSession : AuthSessionData.CreateGuest();
            bool isGuest = session == null || session.isGuest;

            if (accountStateText != null)
            {
                accountStateText.text = isGuest ? "STATUS: GUEST" : "STATUS: CONNECTED";
            }

            if (displayNameText != null)
            {
                displayNameText.text = isGuest ? "GUEST" : session.DisplayName;
            }

            if (emailText != null)
            {
                emailText.text = isGuest
                    ? "NOT SIGNED IN"
                    : (string.IsNullOrWhiteSpace(session.email) ? "NO EMAIL" : session.email);
            }

            if (signOutButton != null)
            {
                signOutButton.gameObject.SetActive(!isGuest);
            }
        }
    }
}
