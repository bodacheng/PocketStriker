using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using dataAccess;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            var timer = new SafeTimer();
            var errorReturning = false;

            async UniTaskVoid ReturnToTitleAsync()
            {
                ProgressLayer.Loading(Translate.Get("ReturnToLobbyForConnectionError"));
                await UniTask.Delay(TimeSpan.FromSeconds(3));
                if (SceneManager.GetActiveScene().buildIndex != 0)
                {
                    SceneManager.LoadScene(0);
                }
            }

            void HandleDataLoadError()
            {
                if (errorReturning)
                {
                    return;
                }

                errorReturning = true;
                timer.Stop();
                ReturnToTitleAsync().Forget();
            }

            ProgressLayer.Loading(string.Empty);
            timer.StartTimer(5, HandleDataLoadError);
            PlayFabReadClient.GetStatistics(StatisticsLoadFinished);
        
            //AccountCharsSet.LoadTutorial();
            PlayFabReadClient.GetMailCatalogItems(PlayFabSetting._MailCatalog, MailCatalogFinished);
            PlayFabReadClient.GetMailCatalogItems(PlayFabSetting._UnitCatalog, UnitCatalogFinished);
            PlayFabReadClient.LoadItems(ItemsLoadFinished);
            PlayFabReadClient.GetAllTitleData(StageRewardFinished);
            PlayFabReadClient.GetAllUserData( new List<string>(){"arcade", "gangbang", "origin", "noAds", PlayFabSetting._timeLimitBuyCode}, ArcadeTFinished);

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
                    timer.Stop();
                    if (errorReturning)
                    {
                        return;
                    }

                    TeamSet.SanitizeAgainstCurrentInventory(GetFocusInstanceID());

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
                },
                () =>
                {
                    HandleDataLoadError();
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
        
        public string GetRandomFocusInstanceID()
        {
            if (dataAccess.Units.Dic == null || dataAccess.Units.Dic.Count == 0)
            {
                return null;
            }

            return dataAccess.Units.Dic.Keys.ElementAt(UnityEngine.Random.Range(0, dataAccess.Units.Dic.Count));
        }
    }
}
