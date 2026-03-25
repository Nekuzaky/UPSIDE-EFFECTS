using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Mindrift.UI
{
    public static class MenuInputSystemUtility
    {
        public static EventSystem EnsureEventSystem()
        {
            EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem eventSystem = systems.Length > 0 ? systems[0] : CreateEventSystem();

            for (int i = 1; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    Object.Destroy(systems[i].gameObject);
                }
            }

            ConfigureInputModule(eventSystem);

            if (EventSystem.current == null || EventSystem.current != eventSystem)
            {
                EventSystem.current = eventSystem;
            }

            return eventSystem;
        }

        private static EventSystem CreateEventSystem()
        {
            GameObject eventSystemGO = new GameObject("EventSystem", typeof(EventSystem));
            return eventSystemGO.GetComponent<EventSystem>();
        }

        private static void ConfigureInputModule(EventSystem eventSystem)
        {
            if (eventSystem == null)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }

            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Object.Destroy(standalone);
            }
#endif
        }
    }
}
