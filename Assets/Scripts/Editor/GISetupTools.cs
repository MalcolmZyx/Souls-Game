using UnityEngine;
using UnityEditor;

// Place this file in any folder named "Editor" (e.g. Assets/Editor/GISetupTools.cs)
public static class GISetupTools
{
    // ---------- Selection-based ----------

    [MenuItem("Tools/GI Setup/Selection — Mark Contribute GI + Static")]
    public static void MarkSelectionContributeGI()
    {
        int count = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            count += ApplyToHierarchy(go, true);
        }
        Debug.Log($"GI Setup: marked {count} objects as ContributeGI + Static.");
    }

    [MenuItem("Tools/GI Setup/Selection — Clear Contribute GI")]
    public static void ClearSelectionContributeGI()
    {
        int count = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            count += ApplyToHierarchy(go, false);
        }
        Debug.Log($"GI Setup: cleared ContributeGI on {count} objects.");
    }

    // ---------- Search by name ----------

    [MenuItem("Tools/GI Setup/By Name Filter…")]
    public static void MarkByName()
    {
        GIFilterWindow.ShowWindow();
    }

    // ---------- Search by component ----------

    [MenuItem("Tools/GI Setup/All MeshRenderers in Scene — Contribute GI")]
    public static void MarkAllMeshRenderers()
    {
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var r in renderers)
            count += SetGIFlags(r.gameObject, true);
        Debug.Log($"GI Setup: marked {count} MeshRenderer objects.");
    }

    // ---------- Core ----------

    public static int ApplyToHierarchy(GameObject root, bool enable)
    {
        int count = 0;
        var transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms)
        {
            // Only touch objects with a renderer — skip empty groups
            if (t.GetComponent<Renderer>() == null) continue;
            count += SetGIFlags(t.gameObject, enable);
        }
        return count;
    }

    public static int SetGIFlags(GameObject go, bool enable)
    {
        var flags = GameObjectUtility.GetStaticEditorFlags(go);

        if (enable)
        {
            flags |= StaticEditorFlags.ContributeGI;
            flags |= StaticEditorFlags.BatchingStatic;
            // Don't force OccluderStatic / OccludeeStatic — those have perf implications
        }
        else
        {
            flags &= ~StaticEditorFlags.ContributeGI;
        }

        GameObjectUtility.SetStaticEditorFlags(go, flags);

        // Also configure the MeshRenderer's receive-GI mode (lightmaps vs light probes)
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && enable)
        {
            var so = new SerializedObject(mr);
            // 0 = Light Probes, 1 = Lightmaps. For static geo you want Lightmaps.
            so.FindProperty("m_ReceiveGI").intValue = 1;
            so.ApplyModifiedProperties();
            mr.scaleInLightmap = 1f;
        }

        EditorUtility.SetDirty(go);
        return 1;
    }
}

// ---------- Name filter window ----------

public class GIFilterWindow : EditorWindow
{
    private string nameContains = "Cliff";
    private bool enable = true;
    private bool caseSensitive = false;

    public static void ShowWindow()
    {
        GetWindow<GIFilterWindow>("GI Name Filter");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Bulk-set Contribute GI by name filter", EditorStyles.boldLabel);
        nameContains = EditorGUILayout.TextField("Name contains", nameContains);
        caseSensitive = EditorGUILayout.Toggle("Case sensitive", caseSensitive);
        enable = EditorGUILayout.Toggle("Enable (uncheck to clear)", enable);

        if (GUILayout.Button("Apply"))
        {
            int count = 0;
            var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in all)
            {
                string a = caseSensitive ? go.name : go.name.ToLowerInvariant();
                string b = caseSensitive ? nameContains : nameContains.ToLowerInvariant();
                if (a.Contains(b))
                    count += GISetupTools.SetGIFlags(go, enable);
            }
            Debug.Log($"GI Setup: applied to {count} objects matching '{nameContains}'.");
        }
    }
}
