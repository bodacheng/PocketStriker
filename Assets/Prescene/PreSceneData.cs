using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using dataAccess;
using UnityEngine;

namespace mainMenu
{
    public partial class PreScene : MonoBehaviour
    {
        protected MissionWatcher missionWatcher;
        
        void StatisticsLoadFinished(bool value)
        {
            missionWatcher.Finish("statisticsFinished", value);
        }
    
        void MailCatalogFinished(bool value)
        {
            missionWatcher.Finish("mailCatalogFinished", value);
        }
    
        void UnitCatalogFinished(bool value)
        {
            missionWatcher.Finish("unitCatalogFinished", value);
        }
    
        void ItemsLoadFinished(bool value)
        {
            missionWatcher.Finish("itemsLoadFinished", value);
        }

        void StageRewardFinished(bool value)
        {
            missionWatcher.Finish("stageRewardsFinished", value);
        }
    
        void ArcadeTFinished(bool value)
        {
            missionWatcher.Finish("arcadeTFinished", value);
        }
        
        public void DataLoading(Action onDataLoad)
        {
            ProgressLayer.Loading(string.Empty);
            PlayFabReadClient.GetStatistics(StatisticsLoadFinished);
        
            //AccountCharsSet.LoadTutorial();
            PlayFabReadClient.GetMailCatalogItems(PlayFabSetting._MailCatalog, MailCatalogFinished);
            PlayFabReadClient.GetMailCatalogItems(PlayFabSetting._UnitCatalog, UnitCatalogFinished);
            PlayFabReadClient.LoadItems(ItemsLoadFinished);
            PlayFabReadClient.GetAllTitleData(StageRewardFinished);
            PlayFabReadClient.GetAllUserData( new List<string>(){"arcade", "gangbang", "noAds", PlayFabSetting._timeLimitBuyCode}, ArcadeTFinished);

            void Next()
            {
                ProgressLayer.Close();
                onDataLoad.Invoke();
                Stones.RenderAll().Forget(); // 在背后运行，从而加快石头列表和技能编辑画面的读取速度
            }
            
            missionWatcher = new MissionWatcher(
                new List<string>
                {
                    "mailCatalogFinished","unitCatalogFinished","itemsLoadFinished", 
                    "statisticsFinished", "arcadeTFinished","stageRewardsFinished"
                },
                () =>
                {
                    switch (PlayerAccountInfo.Me.tutorialProgress)
                    {
                        case "Started":
                            TeamSet.GetTargetSet("arcade").SetPosUnitByInstanceID(0, GetFocusInstanceID());
                            TeamSet.SaveTeamSet("arcade", (x) =>
                            {
                                if (x)
                                {
                                    Next();
                                }
                            });
                            break;
                        case "SkillEditFinished2":
                            var adam = dataAccess.Units.GetByRId("1");
                            TeamSet.GetTargetSet("arcade").SetPosUnitByInstanceID(0, adam.id);
                            TeamSet.SaveTeamSet("arcade", (x) =>
                            {
                                Debug.Log("we are here:"+ adam.id);
                                if (x)
                                {
                                    Next();
                                }
                            });
                            break;
                        default:
                            Next();
                            break;
                    }
                }
            );
        }
        
        public string GetFocusInstanceID()
        {
            string focusInstanceID;
            if (PreScene.target.Focusing != null && dataAccess.Units.Get(PreScene.target.Focusing.id) != null)
            {
                focusInstanceID = PreScene.target.Focusing.id;
            }
            else
            {
                focusInstanceID = PlayerPrefs.GetString("showUnit", null);
                if (string.IsNullOrEmpty(focusInstanceID) || dataAccess.Units.Get(focusInstanceID) == null)
                {
                    foreach (var keyValuePair in dataAccess.Units.Dic)
                    {
                        focusInstanceID = keyValuePair.Key;
                        break;
                    }
                }
            }
            return focusInstanceID;
        }
    }
}