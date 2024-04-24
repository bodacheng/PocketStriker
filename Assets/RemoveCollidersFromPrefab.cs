using UnityEngine;
using UnityEditor;

public class RemoveCollidersWithProgress : EditorWindow
{
    private GameObject prefabObject;
    private LayerMask layerMask;
    private Collider[] collidersToProcess;
    private int currentIndex = 0;
    private bool isProcessing = false;

    [MenuItem("Tools/Remove Colliders Asynchronously")]
    static void ShowWindow()
    {
        GetWindow<RemoveCollidersWithProgress>("Remove Colliders Async");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Select a prefab to remove colliders:", EditorStyles.boldLabel);
        prefabObject = (GameObject)EditorGUILayout.ObjectField("Prefab Object", prefabObject, typeof(GameObject), false);
        layerMask = EditorGUILayout.LayerField("Layers to Keep", layerMask);

        if (GUILayout.Button("Prepare Colliders"))
        {
            PrepareColliders();
        }

        if (GUILayout.Button("Start Removing"))
        {
            StartRemovingColliders();
        }

        if (isProcessing)
        {
            EditorGUILayout.LabelField("Processing... " + currentIndex + "/" + collidersToProcess.Length);
        }
    }

    private void PrepareColliders()
    {
        if (prefabObject != null)
        {
            collidersToProcess = prefabObject.GetComponentsInChildren<Collider>(true);
            Debug.Log("Prepared " + collidersToProcess.Length + " colliders for processing.");
        }
        else
        {
            Debug.LogError("No prefab selected. Please select a prefab first.");
        }
    }

    private void StartRemovingColliders()
    {
        if (collidersToProcess != null)
        {
            currentIndex = 0;
            isProcessing = true;
            EditorApplication.update += ProcessStep;
        }
        else
        {
            Debug.LogError("No colliders prepared. Please prepare colliders first.");
        }
    }

    private void ProcessStep()
    {
        if (currentIndex < collidersToProcess.Length)
        {
            Collider collider = collidersToProcess[currentIndex];
            if (collider != null && ((1 << collider.gameObject.layer) & layerMask.value) == 0)
            {
                Undo.DestroyObjectImmediate(collider);
            }
            currentIndex++;
        }
        else
        {
            EditorApplication.update -= ProcessStep;
            isProcessing = false;
            Debug.Log("All colliders have been removed.");
            EditorUtility.ClearProgressBar();
        }
        EditorUtility.DisplayProgressBar("Removing Colliders", "Progress: " + currentIndex + " / " + collidersToProcess.Length, currentIndex / (float)collidersToProcess.Length);
    }
}
