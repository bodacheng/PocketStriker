using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Legacy gang-battle story API backed by the shared localization table.</summary>
public static class GBShortStory
{
    public static bool IsLoaded() => Story.IsLoaded();
    public static List<Story.Row> GetRowList() => Story.GetRowList();
    public static int NumRows() => Story.NumRows();
    public static Story.Row GetAt(int index) => Story.GetAt(index);
    public static string Get(string languageCode) => Story.Get(languageCode);

    public static UniTask LoadLanguageCodes()
    {
        return Story.LoadLanguageCodes(key => AddressablesLogic.LoadT<TextAsset>(key));
    }

    public static Story.Row Find_RECORD_ID(string value) => Story.Find_RECORD_ID(value);
    public static List<Story.Row> FindAll_RECORD_ID(string value) => Story.FindAll_RECORD_ID(value);
    public static Story.Row Find_EN(string value) => Story.Find_EN(value);
    public static List<Story.Row> FindAll_EN(string value) => Story.FindAll_EN(value);
    public static Story.Row Find_JP(string value) => Story.Find_JP(value);
    public static List<Story.Row> FindAll_JP(string value) => Story.FindAll_JP(value);
    public static Story.Row Find_CH(string value) => Story.Find_CH(value);
    public static List<Story.Row> FindAll_CH(string value) => Story.FindAll_CH(value);
}
