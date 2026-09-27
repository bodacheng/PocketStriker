using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal static class LocalizationTableUtility
{
    public static UniTask<TextAsset> LoadCsv(
        string key,
        Func<string, UniTask<TextAsset>> load = null)
    {
        return load != null
            ? load(key)
            : AddressablesAssetLoader.LoadT<TextAsset>(key);
    }

    public static void PopulateRows<TRow>(
        TextAsset csv,
        List<TRow> rows,
        Func<string[], TRow> createRow,
        Action<TRow> onRow = null)
    {
        rows.Clear();
        var grid = CsvParser2.Parse(csv.text);
        for (var i = 1; i < grid.Length; i++)
        {
            if (grid[i].Length != 4)
            {
                continue;
            }

            var row = createRow(grid[i]);
            onRow?.Invoke(row);
            rows.Add(row);
        }
    }

    public static string SelectLanguage(
        SystemLanguage language,
        string english,
        string japanese,
        string chinese)
    {
        switch (language)
        {
            case SystemLanguage.Japanese:
                return japanese;
            case SystemLanguage.Chinese:
                return chinese;
            case SystemLanguage.English:
            default:
                return english;
        }
    }
}
