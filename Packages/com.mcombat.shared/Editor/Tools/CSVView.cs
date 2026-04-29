using UnityEditor;
using UnityEngine;

public class CSVView : EditorWindow
{
    TextAsset csv;
    string[][] arr;

    [MenuItem("Window/CSV View")]
    public static void ShowWindow()
    {
        GetWindow(typeof(CSVView));
    }

    void OnGUI()
    {
        var newCsv = EditorGUILayout.ObjectField("CSV", csv, typeof(TextAsset), false) as TextAsset;
        if (newCsv != csv)
        {
            csv = newCsv;
            arr = csv != null ? CsvParser2.Parse(csv.text) : null;
        }

        if (GUILayout.Button("Refresh") && csv != null)
        {
            arr = CsvParser2.Parse(csv.text);
        }

        if (csv == null)
        {
            return;
        }

        if (arr == null)
        {
            arr = CsvParser2.Parse(csv.text);
        }

        for (var i = 0; i < arr.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();
            for (var j = 0; j < arr[i].Length; j++)
            {
                EditorGUILayout.TextField(arr[i][j]);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
