using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mindrift.UI
{
    public sealed class MenuNavigationController : MonoBehaviour
    {
        [SerializeField] private Selectable defaultSelected;
        [SerializeField] private Selectable[] verticalNavigationOrder;
        [SerializeField] private bool selectDefaultOnEnable = true;

        private void Awake()
        {
            if (verticalNavigationOrder != null && verticalNavigationOrder.Length > 1)
            {
                ApplyVerticalNavigation(verticalNavigationOrder);
            }
        }

        private void OnEnable()
        {
            if (selectDefaultOnEnable)
            {
                SelectDefault(this, defaultSelected);
            }
        }

        public void SetDefault(Selectable selectable)
        {
            defaultSelected = selectable;
        }

        public static void SelectDefault(MonoBehaviour host, Selectable selectable)
        {
            if (host == null || selectable == null)
            {
                return;
            }

            host.StartCoroutine(SelectOnNextFrame(selectable));
        }

        public static void ApplyVerticalNavigation(IReadOnlyList<Selectable> selectables)
        {
            if (selectables == null || selectables.Count == 0)
            {
                return;
            }

            for (int i = 0; i < selectables.Count; i++)
            {
                Selectable current = selectables[i];
                if (current == null)
                {
                    continue;
                }

                Navigation navigation = current.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = FindPrevious(selectables, i);
                navigation.selectOnDown = FindNext(selectables, i);
                navigation.selectOnLeft = navigation.selectOnUp;
                navigation.selectOnRight = navigation.selectOnDown;
                current.navigation = navigation;
            }
        }

        private static Selectable FindPrevious(IReadOnlyList<Selectable> selectables, int startIndex)
        {
            for (int i = startIndex - 1; i >= 0; i--)
            {
                if (selectables[i] != null && selectables[i].IsInteractable())
                {
                    return selectables[i];
                }
            }

            return null;
        }

        private static Selectable FindNext(IReadOnlyList<Selectable> selectables, int startIndex)
        {
            for (int i = startIndex + 1; i < selectables.Count; i++)
            {
                if (selectables[i] != null && selectables[i].IsInteractable())
                {
                    return selectables[i];
                }
            }

            return null;
        }

        private static IEnumerator SelectOnNextFrame(Selectable selectable)
        {
            yield return null;

            if (selectable == null || EventSystem.current == null)
            {
                yield break;
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            selectable.Select();
        }
    }
}
