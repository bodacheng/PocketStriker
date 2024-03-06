//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using System.IO;
//using System.Xml;
//using System.Xml.Serialization;
//using System;
//using System.Linq;
//#if UNITY_EDITOR
//using UnityEditor;
//#endif
//using UnityEngine.UI;

//[Serializable]
//public class AISeries //这个class的存在无非是我们试图对数据库的一种描述。。。
//{
//	public int AISeriesID;//这个值是针对于一个type的AIDATABASE里的索引
//    public string type;
//    public string ScriptName;
//	public string AIScriptShowName;
//}

//public class levelAndCurrentExp
//{
//    public int level;
//    public int currentExpInLevel;
//	public levelAndCurrentExp(int level,int currentExpInLevel)
//	{
//		this.level = level;
//		this.currentExpInLevel = currentExpInLevel;
//	}
//}

//[System.Serializable]
//public class AIBeheviourInfo
//{
 //   public int AI_ID;
 //   public string type;
	//public string scriptName;//我们姑且认为所有AI脚本不管什么type都是放在一起
 //   public string AIScriptShowName;

 //   public int level;
	//public int Exp;

	////这是个纠结的话就会痛苦的东西，最好痛快点。

	////public int exp;
	////   public int Exp
	////{
	////	set
	////	{
	////		exp = value;
	////		isLevelDirty = true;
	////	}
	////}
	////public int level;
	////public bool isLevelDirty;
	////public int Level
	////{ 
	////	get 
	////	{ 
	////		if(isLevelDirty)
	////		{
	////			level = calculateLevel();
	////			isLevelDirty = false;
	////		}
	////		return level;
	////	}
	////}

	//AIBeheviourInfo()
	//{
	//}
        
	//public levelAndCurrentExp returnCurrentLevel()
	//{
 //       //int tempExp = this.Exp;
 //       //int level = 1;
 //       //while (!(tempExp < 100))
 //       //{
 //       //	if (level >= 1 && level < 20)
 //       //          {
 //       //		if (tempExp >= 100)
 //       //		{
 //       //			tempExp -= 100;
 //       //			level += 1;
 //       //		}
 //       //          }
 //       //	else if (level >= 20 && level < 40)
 //       //          {
 //       //		if (tempExp >= 150)
 //       //              {
 //       //                  tempExp -= 150;
 //       //                  level += 1;
 //       //              }
 //       //          }
 //       //	else if (level >= 40 && level < 60)
 //       //          {
 //       //		if (tempExp >= 200)
 //       //              {
 //       //                  tempExp -= 200;
 //       //                  level += 1;
 //       //              }
 //       //          }
 //       //	else if (level >= 60 && level < 80)
 //       //          {
 //       //		if (tempExp >= 250)
 //       //              {
 //       //                  tempExp -= 250;
 //       //                  level += 1;
 //       //              }
 //       //          }
 //       //	else if (level >= 80 && level < 100)
 //       //          {
 //       //		if (tempExp >= 300)
 //       //              {
 //       //                  tempExp -= 300;
 //       //                  level += 1;
 //       //              }
 //       //          }
 //       //	else if (level == 100)
 //       //          {

 //       //          }
 //       //}
 //       //levelAndCurrentExp _return = new levelAndCurrentExp(level,tempExp);
 //       //return _return;

 //       levelAndCurrentExp _return = new levelAndCurrentExp(level, this.Exp);
 //       return _return;
	//}
       
	//public int ExpPlus(int plusEx)
	//{
	//	this.Exp += plusEx;
	//	return Exp;
	//}

	//public int wholeExpAtCurrentLevel(int scriptLevel)
	//{
	//	if (scriptLevel >= 1 && scriptLevel <20)
	//	{
	//		return 100;
	//	}
	//	else if (scriptLevel >= 20 && scriptLevel < 40)
	//	{
	//		return 150;
	//	}
	//	else if (scriptLevel >= 40 && scriptLevel < 60)
 //       {
	//		return 200;
 //       }
	//	else if (scriptLevel >= 60 && scriptLevel < 80)
 //       {
	//		return 250;
 //       }
	//	else if (scriptLevel >= 80 && scriptLevel < 100)
 //       {
	//		return 300;
 //       }
	//	return 100;
	//}

  //  public AIBeheviourInfo(int localAINum,int scriptSeriesNum, int Exp)
  //  {
  //      this.localAINum = localAINum;
  //      this.scriptSeriesNum = scriptSeriesNum;
		//this.Exp = Mathf.Clamp(Exp,0,19900);
    //}

 //   public AIBeheviourInfo(string scriptName,int targetlevel)
	//{
 //       this.scriptName = scriptName;
                
	//	if (targetlevel >= 1 && targetlevel < 20)
 //       {
	//		this.Exp = (targetlevel - 1) * 100;
 //       }
	//	else if (targetlevel >= 20 && targetlevel < 40)
 //       {
	//		this.Exp = 100 * 19 + (targetlevel - 20) * 150;
 //       }
	//	else if (targetlevel >= 40 && targetlevel < 60)
 //       {
	//		this.Exp = 100 * 19 + 150 * 20 + (targetlevel - 40) * 200;
 //       }
	//	else if (targetlevel >= 60 && targetlevel < 80)
 //       {
	//		this.Exp = 100 * 19 + 150 * 20 + 200 * 20 + (targetlevel - 60) * 250;
 //       }
	//	else if (targetlevel >= 80 && targetlevel < 100)
 //       {
	//		this.Exp = 100 * 19 + 150 * 20 + 200 * 20 + 20 * 250 + (targetlevel - 80) * 300;
 //       }
	//	else if (targetlevel == 100)
 //       {
	//		this.Exp = 100 * 19 + 150 * 20 + 200 * 20 + 20 * 250 + 20 * 300;
 //       }
	//}

    //public static AIBeheviourInfo getOneAIBeheviourInfo(AISeries _AISeries,int level)
    //{
    //    AIBeheviourInfo _AIBeheviourInfo = new AIBeheviourInfo(_AISeries.ScriptName, level);
    //    return _AIBeheviourInfo;
    //}
	//缺乏根据玩家等级生成敌人AI等级的函数
//}
//AIBeheviourInfo是对一个角色所拥有的一个AI脚本的全面形容，这个信息在我们前一个月引入了角色type后应该有相应的变化。
//

//namespace UnityEngine.UI
//{
//	public class AIDataBase : ScriptableObject {
//        public string type;
//		public AISeries[] series;

//		public AISeries Get(int index)
//		{
//			return (this.series[index]);
//		}
			
//		public AISeries GetByID(int ID)
//		{
//			for (int i = 0; i < this.series.Length; i++)
//			{
//				if (this.series[i].AISeriesID == ID)
//					return this.series[i];
//			}
//			return null;
//		}

//        public List<int> getAllAISeriesIDs()
//        {
//            List<int> IDs = new List<int>();
//            foreach (AISeries _AISeries in series)
//            {
//                if (!IDs.Contains(_AISeries.AISeriesID))
//                {
//                    IDs.Add(_AISeries.AISeriesID);
//                }
//            }
//            return IDs;
//        }

//        public List<string> getALLAISeriesNames()
//        {
//            List<string> names = new List<string>();
//            foreach (AISeries _AISeries in series)
//            {   
//                if (_AISeries.ScriptName != null)
//                {
//                    if (!names.Contains(_AISeries.ScriptName))
//                    {
//                        names.Add(_AISeries.ScriptName);
//                    }
//                }else{
//                    Debug.Log(_AISeries+"脚本系列名为设置，必须设置脚本系列名！");   
//                }
//            }
//            return names;
//        }

//        //public AISeries getAISeriesBySeriesName(string SeriesName)
//        //{
//        //    foreach (AISeries one in series)
//        //    {
//        //        if (one.ScriptName.GetHashCode() == SeriesName.GetHashCode())
//        //        {
//        //            return one;
//        //        }
//        //    }
//        //    return null;
//        //}

//		public AISeries RandomGetAISeries()
//		{
//			int _aiNum = Random.Range (0,series.Length);
//			return series[_aiNum];
//		}       
//	}
//}

//public class AIDataBaseEditor
//{
//	#if UNITY_EDITOR
//	private static string GetSavePath()
//	{
//		return EditorUtility.SaveFilePanelInProject("New AISeries database", "New AISeries database", "asset", "Create a new AISeries database.");
//	}
		
//	[MenuItem("Assets/Create/Databases/AISeries Database")]
//	public static void CreateDatabase()
//	{
//		string assetPath = GetSavePath();
//		AIDataBase asset = ScriptableObject.CreateInstance("AIDataBase") as AIDataBase;  //scriptable object
//		AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(assetPath));
//		AssetDatabase.Refresh();
//	}
//	#endif
//}