using System.Collections.Generic;
using mainMenu;
using UnityEngine;

public class TutorialRunner
{
    static TutorialRunner _instance;
    public static TutorialRunner Main
    {
        get
        {
            if (_instance == null)
            {
                _instance = new TutorialRunner();
            }
            return _instance;
        }
    }
    
    TutorialProcess currentProcess;
    private readonly List<TutorialProcess> _tutorialProcesses = new List<TutorialProcess>();
    
    void GenerateStep1Tutorial()
    {
        var goToUnitList = new GoTo(MainSceneStep.UnitList);
        var openSkillEdit = new OpenSkillEdit("3");
        var skillEditTry = new SkillEditTry("openInstruction1");
        var explainCombo = new ExplainCombo();
        _tutorialProcesses.Clear();
        _tutorialProcesses.Add(goToUnitList);
        _tutorialProcesses.Add(openSkillEdit);
        _tutorialProcesses.Add(skillEditTry);
        _tutorialProcesses.Add(explainCombo);
    }
    
    void GenerateStep2Tutorial()
    {
        var goTo1 = new GoTo(MainSceneStep.FrontPage);
        var goTo2 = new GoTo(MainSceneStep.QuestInfo);
        var teamEdit1 = new TeamEdit("teamEdit1");
        
        bool StageOneFinished()
        {
            return PlayerAccountInfo.Me.tutorialProgress == "StageOneFinished";
        }
        
        var waitFighting = new WaitProcess(StageOneFinished);
        
        _tutorialProcesses.Clear();
        _tutorialProcesses.Add(goTo1);
        _tutorialProcesses.Add(goTo2);
        _tutorialProcesses.Add(teamEdit1);
        _tutorialProcesses.Add(waitFighting);
    }

    void GenerateStep3Tutorial()
    {
        var goTo = new GoTo(MainSceneStep.GotchaFront);
        var tryGotcha = new TryGotcha();
        
        _tutorialProcesses.Clear();
        _tutorialProcesses.Add(goTo);
        _tutorialProcesses.Add(tryGotcha);
    }

    void GenerateStep4Tutorial()
    {
        var goTo = new GoTo(MainSceneStep.UnitList);
        var openSkillEdit = new OpenSkillEdit("1");
        var skillEditTry = new SkillEditTry("openInstruction2");
        _tutorialProcesses.Clear();
        _tutorialProcesses.Add(goTo);
        _tutorialProcesses.Add(openSkillEdit);
        _tutorialProcesses.Add(skillEditTry);
    }
    
    void GenerateStep5Tutorial()
    {
        var goTo = new GoTo(MainSceneStep.QuestInfo);
        //var forceTeamEdit = new ForceToTeamEdit("teamEdit2");
        var teamEdit2 = new TeamEdit("teamEdit2");

        _tutorialProcesses.Clear();
        _tutorialProcesses.Add(goTo);
        //_tutorialProcesses.Add(forceTeamEdit);
        _tutorialProcesses.Add(teamEdit2);
    }
    
    public void Process()
    {
        if (currentProcess != null)
        {
            currentProcess.LocalUpdate();
            if (currentProcess.CanEnterOtherProcess()) // && currentProcess.nextProcessStep != MainSceneStep.None
            {
                MoveToNext();
            }
        }
    }
    
    public void MoveToNext()
    {
        var index = 0;
        TutorialProcess toBeRemove = currentProcess;
        if (currentProcess != null)
        {
            index = _tutorialProcesses.IndexOf(currentProcess)+ 1;
            toBeRemove = currentProcess;
        }
        var toRunProcess = _tutorialProcesses.Count > index ? _tutorialProcesses[index] : null;
        ChangeProcess(toRunProcess);
        if (toBeRemove != null)
            _tutorialProcesses.Remove(toBeRemove);
    }
    
    void ChangeProcess(TutorialProcess nextProcess)
    {
        currentProcess?.ProcessEnd();
        currentProcess = nextProcess;
        if (currentProcess != null)
        {
            currentProcess.ProcessEnter();
        }
        else
            TutorialCheck();
    }
    
    // 所有的教程链都是以FrontPage为起点
    public void TutorialCheck()
    {
        // 在以下的分歧之前，账户信息必须是最新，否则反应不到账户真实进度。
        switch (PlayerAccountInfo.Me.tutorialProgress)
        {
            case "Started":
                Main.GenerateStep1Tutorial();
                Main.MoveToNext();
                PlayFabReadClient.DontShowFrontFight = "false";
                break;
            case "SkillEditFinished": // 技能编辑教程结束 
                Main.GenerateStep2Tutorial();
                Main.MoveToNext();
                break;
            case "StageOneFinished": // 第一关结束
                GenerateStep3Tutorial();
                Main.MoveToNext();
                break;
            case "GotchaFinished":
                var _focusUnitInfo = dataAccess.Units.GetByRId("1");
                if (_focusUnitInfo != null)
                    PreScene.target.SetFocusingUnit(_focusUnitInfo.id);
                GenerateStep4Tutorial();
                Main.MoveToNext();
                break;
            case "SkillEditFinished2":
                GenerateStep5Tutorial();
                Main.MoveToNext();
                break;
            case "Finished":
                PlayFabReadClient.DontShowFrontFight = "true";
                break;
            default:
                break;
        }
    }
}
