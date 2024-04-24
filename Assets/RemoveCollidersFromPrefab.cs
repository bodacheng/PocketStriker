using UnityEngine;
using UnityEditor;

public class RemoveCollidersFromPrefab : EditorWindow
{
    GameObject prefabObject;
    LayerMask layerMask;
    Collider[] collidersToProcess;
    int colliderCount = 0;

    [MenuItem("Tools/Remove Colliders With Efficiency")]
    public static void ShowWindow()
    {
        GetWindow<RemoveCollidersFromPrefab>("Remove Colliders Efficiently");
    }

    void OnGUI()
    {
        GUILayout.Label("Select a prefab to modify colliders", EditorStyles.boldLabel);
        prefabObject = (GameObject)EditorGUILayout.ObjectField("Prefab Object", prefabObject, typeof(GameObject), false);
        layerMask = EditorGUILayout.LayerField("Layers to Keep", layerMask);

        if (GUILayout.Button("Count and Cache Colliders"))
        {
            if (prefabObject != null)
            {
                collidersToProcess = prefabObject.GetComponentsInChildren<Collider>(true);
                colliderCount = collidersToProcess.Length;
                Debug.Log("Total colliders in prefab: " + colliderCount);
            }
            else
            {
                Debug.LogError("No prefab selected. Please select a prefab first.");
            }
        }

        if (GUILayout.Button("Remove Colliders"))
        {
            if (prefabObject != null && collidersToProcess != null)
            {
                RemoveCollidersWithProgress(collidersToProcess, layerMask);
                Debug.Log("Colliders have been removed efficiently from the prefab.");
            }
            else
            {
                Debug.LogError("No prefab or collider cache found. Please cache colliders first.");
            }
        }

        GUILayout.Space(20);
        GUILayout.Label("Total Colliders: " + colliderCount);
    }

    private void RemoveCollidersWithProgress(Collider[] colliders, LayerMask layersToKeep)
    {
        int updateStep = Mathf.Max(1, colliders.Length / 10); // Update progress every 10% of the way
        Undo.RecordObjects(colliders, "Remove Colliders");

        for (int i = 0; i < colliders.Length; i++)
        {
            if (((1 << colliders[i].gameObject.layer) & layersToKeep.value) == 0)
            {
                Undo.DestroyObjectImmediate(colliders[i]);
            }

            if (i % updateStep == 0)
            {
                EditorUtility.DisplayProgressBar("Removing Colliders", "Progress: " + (i + 1) + " / " + colliders.Length, i / (float)colliders.Length);
            }
        }
        EditorUtility.ClearProgressBar();
    }
}
