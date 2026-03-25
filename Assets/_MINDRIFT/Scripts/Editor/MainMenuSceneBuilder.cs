#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mindrift.UI;

namespace Mindrift.Editor
{
    public static class MainMenuSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayScenePath = "Assets/Scenes/Games.unity";

        [MenuItem("Tools/MINDRIFT/Create Or Refresh Main Menu Scene")]
        public static void CreateOrRefresh()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera mainCamera = Object.FindFirstObjectByType<Camera>();
            if (mainCamera != null)
            {
                mainCamera.backgroundColor = new Color(0.03f, 0.04f, 0.08f, 1f);
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
            }

            EnsureEventSystem();

            GameObject canvasGO = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = canvasGO.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject background = CreateUIObject("Background", canvasGO.transform);
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.02f, 0.02f, 0.05f, 0.95f);
            Stretch(background.GetComponent<RectTransform>());

            GameObject title = CreateText("Title", canvasGO.transform, "MINDRIFT", 96, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 1f, 1f));
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.78f);
            titleRect.anchorMax = new Vector2(0.5f, 0.78f);
            titleRect.sizeDelta = new Vector2(1100f, 150f);
            titleRect.anchoredPosition = Vector2.zero;

            GameObject subtitle = CreateText("Subtitle", canvasGO.transform, "CLIMB INTO COGNITIVE NOISE", 28, TextAnchor.MiddleCenter, new Color(0.45f, 0.95f, 1f, 0.95f));
            RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0.5f, 0.68f);
            subtitleRect.anchorMax = new Vector2(0.5f, 0.68f);
            subtitleRect.sizeDelta = new Vector2(1200f, 80f);
            subtitleRect.anchoredPosition = Vector2.zero;

            Button startButton = CreateButton("StartButton", canvasGO.transform, "START RUN", new Vector2(0.5f, 0.48f), new Vector2(380f, 86f));
            Button quitButton = CreateButton("QuitButton", canvasGO.transform, "QUIT", new Vector2(0.5f, 0.36f), new Vector2(380f, 86f));

            MainMenuController controller = canvasGO.AddComponent<MainMenuController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.FindProperty("gameplaySceneName").stringValue = "Games";
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MINDRIFT] Main menu scene created at {ScenePath}.");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        private static void AddScenesToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            AddOrEnableScene(scenes, ScenePath);
            AddOrEnableScene(scenes, GameplayScenePath);

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddOrEnableScene(List<EditorBuildSettingsScene> scenes, string path)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path)
                {
                    scenes[i] = new EditorBuildSettingsScene(path, true);
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateText(string name, Transform parent, string text, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject go = CreateUIObject(name, parent);
            Text uiText = go.AddComponent<Text>();
            uiText.text = text;
            uiText.alignment = alignment;
            uiText.fontSize = fontSize;
            uiText.color = color;
            uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            uiText.raycastTarget = false;
            return go;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchor, Vector2 size)
        {
            GameObject buttonGO = CreateUIObject(name, parent);
            RectTransform rect = buttonGO.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.12f, 0.18f, 0.25f, 0.92f);

            Button button = buttonGO.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.25f, 0.45f, 0.6f, 1f);
            colors.pressedColor = new Color(0.1f, 0.25f, 0.35f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
            button.colors = colors;

            GameObject labelGO = CreateText("Label", buttonGO.transform, label, 32, TextAnchor.MiddleCenter, new Color(0.95f, 0.98f, 1f, 1f));
            Stretch(labelGO.GetComponent<RectTransform>());

            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
