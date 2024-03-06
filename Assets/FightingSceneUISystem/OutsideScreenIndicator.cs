//该系统的构成应该包括这些方面
//1.对双方队伍成员可以进行一个把握，企划上我们可能不需要指示屏幕范围外队友的位置，但原则上这个模块可以把握全队。    
//2.判断角色和控制中角色的屏幕范围内连线，算出此连线与屏幕四周的交叉点，
//3.为该交叉点适配一个贴图一样的东西，可以考虑加上角色名字，

//using System.Collections.Generic;
//using UnityEngine;

//public class OutsideScreenIndicator : MonoBehaviour {
    
//    public CharsManager _CharSetManager;
//    public CameraManager _CameraManager;
//    public Texture icon;
	
//    void OnGUI()
//    {
//    	ShowEnemyOutsideMapIndicator();
//    }

//    IDictionary<Team, List<Data_Center>> TeamDataCeneterList;
//	List<Data_Center> Data_Centers;
//    void ShowEnemyOutsideMapIndicator()
//    {
//        if (TeamDataCeneterList == null)
//            return;
//        TeamDataCeneterList.TryGetValue(Team.player2, out Data_Centers);
//        if (Data_Centers == null)
//            return;
            
//        foreach (Data_Center _one in Data_Centers)
//        {
//            ShowPosIndicator(_one);
//        }
//    }
    
//    bool outOfScreen;
//    Vector3 pos;
//    void ShowPosIndicator(Data_Center one)//引数是datacenter是考虑到可能想显示角色名字一类
//    {
//        pos = CameraManager._camera.WorldToScreenPoint(one.gameObject.transform.position);
//        outOfScreen = false;
//        outOfScreen |= pos.x < 0.0;
//        outOfScreen |= Screen.width < pos.x;
//        outOfScreen |= pos.y < 0.0;
//        outOfScreen |= Screen.height < pos.y;
        
//        if (outOfScreen)
//        {
//            if (pos.y > Screen.height) { // to the top
//            	pos.y = Screen.height;
//            } 
//            else if (pos.y < 0) { // to the bottom
//                pos.y = 0f; // height of the texture 
//            }
//            if (pos.x > Screen.width) { // to the right
//            	pos.x = Screen.width;
//            } 
//            else if (pos.x < 0) { // just right behind me or my left
//                pos.x = 0;               
//            }
            
//            Rect rect = new Rect(pos.x-30, Screen.height - pos.y-30,60,60);
//            GUI.DrawTexture(rect, icon, ScaleMode.StretchToFill, true, 0);
//        }
//    }
//}