using System;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public static class GBShortStory
{
	public class Row
	{
		public string RECORD_ID;
		public string EN;
		public string JP;
		public string CH;
	}

	static readonly List<Row> rowList = new List<Row>();
	static bool isLoaded = false;

	public static bool IsLoaded()
	{
		return isLoaded;
	}

	public static List<Row> GetRowList()
	{
		return rowList;
	}
    
    public static async UniTask LoadLanguageCodes()
    {
	    var csv = await AddressablesLogic.LoadT<TextAsset>("Config/" + CommonSetting.GBShortStoryFile);
	    if (csv != null)
        {
            Load(csv);
        }
    }

    
    static void Load(TextAsset csv)
	{
		rowList.Clear();
		string[][] grid = CsvParser2.Parse(csv.text);
		for(int i = 1 ; i < grid.Length ; i++)
		{
			if (grid[i].Length == 4)
			{
				var row = new Row
				{
					RECORD_ID = grid[i][0],
					EN = grid[i][1],
					JP = grid[i][2],
					CH = grid[i][3]
				};
				rowList.Add(row);
			}
		}
		isLoaded = true;
	}

	public static int NumRows()
	{
		return rowList.Count;
	}

	public static Row GetAt(int i)
	{
		if(rowList.Count <= i)
			return null;
		return rowList[i];
	}

	public static string Get(string languageCode)
	{
		if (String.IsNullOrEmpty(languageCode))
			return null;
		var row = Find_RECORD_ID(languageCode);
		string text = default;
		if (row != null)
		{
			switch (AppSetting.Value.Language)
			{
				case SystemLanguage.English:
					text = row.EN;
					break;
				case SystemLanguage.Japanese:
					text = row.JP;
					break;
				case SystemLanguage.Chinese:
					text = row.CH;
					break;
				default:
					text = row.EN;
					break;
			}
		}
		return text;
	}

	public static Row Find_RECORD_ID(string find)
	{
		return rowList.Find(x => x.RECORD_ID == find);
	}
	public static List<Row> FindAll_RECORD_ID(string find)
	{
		return rowList.FindAll(x => x.RECORD_ID == find);
	}
	public static Row Find_EN(string find)
	{
		return rowList.Find(x => x.EN == find);
	}
	public static List<Row> FindAll_EN(string find)
	{
		return rowList.FindAll(x => x.EN == find);
	}
	public static Row Find_JP(string find)
	{
		return rowList.Find(x => x.JP == find);
	}
	public static List<Row> FindAll_JP(string find)
	{
		return rowList.FindAll(x => x.JP == find);
	}
	public static Row Find_CH(string find)
	{
		return rowList.Find(x => x.CH == find);
	}
	public static List<Row> FindAll_CH(string find)
	{
		return rowList.FindAll(x => x.CH == find);
	}

}