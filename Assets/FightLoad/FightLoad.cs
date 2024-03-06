using UnityEngine.SceneManagement;
using mainMenu;
using UnityEngine;
using FightScene;

public static class FightLoad
{
    public static FightInfo Fight;
    
    public static void Go(FightInfo fightInfo, bool inSceneLoad = false)
    {
        switch (fightInfo.EventType)
        {
            case FightEventType.Screensaver:
            case FightEventType.SkillTest:
            case FightEventType.Gangbang:
                fightInfo.Team1Auto = true;
                fightInfo.Team2Auto = true;
                break;
            default:
                fightInfo.Team1Auto = PlayerPrefs.GetInt("auto", 0) == 1;
                fightInfo.Team2Auto = true;
                break;
        }
        
        if (fightInfo.ID == "1" && fightInfo.EventType == FightEventType.Quest)
        {
            fightInfo.RunTutorial = true;
            fightInfo.Team1Auto = false;
            fightInfo.Team2Auto = false;
        }
        
        Fight =  FightInfo.Copy(fightInfo);

        if (!inSceneLoad)
        {
            PreScene.CashClear();
            SceneManager.LoadScene(2);
        }
        else
        {
            FSceneProcessesRunner.Main.ChangeProcess(SceneStep.Preparing);
        }
    }
    
    
}
