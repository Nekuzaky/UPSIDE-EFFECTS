#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RGBBrainrotHierarchyTools
{
    [MenuItem("Tools/RGB Brainrot/Create Or Refresh In Hierarchy")]
    private static void CreateOrRefresh()
    {
        RGBBrainrotBootstrap bootstrap = Object.FindFirstObjectByType<RGBBrainrotBootstrap>();
        if (bootstrap == null)
        {
            GameObject host = new("RGB Brainrot Bootstrap");
            Undo.RegisterCreatedObjectUndo(host, "Create RGB Brainrot Bootstrap");
            bootstrap = host.AddComponent<RGBBrainrotBootstrap>();
        }

        Undo.RegisterFullObjectHierarchyUndo(bootstrap.gameObject, "Generate RGB Brainrot");
        bootstrap.GenerateInHierarchyEditor();
        Selection.activeGameObject = bootstrap.gameObject;
        EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
    }

    [MenuItem("Tools/RGB Brainrot/Clear Generated Hierarchy")]
    private static void ClearGenerated()
    {
        RGBBrainrotBootstrap bootstrap = Object.FindFirstObjectByType<RGBBrainrotBootstrap>();
        if (bootstrap == null)
        {
            Debug.LogWarning("RGB Brainrot: no bootstrap found in the scene.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(bootstrap.gameObject, "Clear RGB Brainrot");
        bootstrap.ClearGeneratedInHierarchyEditor();
        Selection.activeGameObject = bootstrap.gameObject;
        EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
    }
}
#endif
