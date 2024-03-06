using dataAccess;
using mainMenu;
using DummyLayerSystem;
using System.Collections.Generic;
using PlayFab.ClientModels;

public class TeamEdit : TutorialProcess
{
    private TeamEditLayer _teamEditLayer;
    private ReturnLayer _returnLayer;
    private FightPrepareLayer _fightPrepareLayer;
    private TeamEditPage _teamEditPage;
    private bool _teamEditFinished = false;
    private readonly string _tutorialStep;
    public TeamEdit(string tutorialStep)
    {
        _tutorialStep = tutorialStep;
    }
    
    public override void ProcessEnter()
    {
        _teamEditPage = (TeamEditPage)ProcessesRunner.Main.GetProcess(MainSceneStep.TeamEditFront);
        _teamEditPage.SetExtraArcadeTeamEditSuccess(
            () =>
            {
                if (_tutorialStep == "teamEdit2") // 总教程结束于第二次队伍编辑结束
                {
                    PlayFabReadClient.UpdateUserData(
                        new UpdateUserDataRequest()
                        {
                            Data = new Dictionary<string, string>()
                            {
                                { "TutorialProgress", "Finished" }
                            }
                        },
                        () =>
                        {
                            PlayerAccountInfo.Me.tutorialProgress = "Finished";
                        }
                    );
                }
                
                _teamEditFinished = true;
                if (_returnLayer != null)
                {
                    _returnLayer.gameObject.SetActive(false);
                    //_returnLayer.ForceBackMode(true);
                }
            }
        );
    }
    
    private bool TutorialLegal(string teamMode)
    {
        bool qualified = true;
        int unitCount = 0;

        PosKeySet targetTeamSet = null;
        switch (teamMode)
        {
            case "arena":
                targetTeamSet = TeamSet.Arena3V3;
                break;
            case "arcade":
                targetTeamSet = TeamSet.Default;
                break;
            case "gangbang":
                targetTeamSet = TeamSet.Gangbang;
                break;
        }
        
        foreach (var set in targetTeamSet.PosNumsWithLocalKeys)
        {
            if (set.instanceID != null && dataAccess.Units.Get(set.instanceID) != null)
            {
                qualified = qualified && (Stones.GetEquippingStones(set.instanceID).Count == 9);
                unitCount += 1;
            }
            if (!qualified)
                break;
        }
        
        switch (teamMode)
        {
            case "arena":
                qualified = qualified && unitCount == 3;
                break;
            case "arcade":
                if (_tutorialStep == "teamEdit1")
                {
                    qualified = qualified && unitCount > 0;
                }
                else if (_tutorialStep == "teamEdit2")
                {
                    qualified = qualified && unitCount > 1;
                }
                break;
            case "gangbang":
                return true;
        }
        return qualified;
    }
    
    public override bool CanEnterOtherProcess()
    {
        return _teamEditFinished;
    }
    
    public override void LocalUpdate()
    {
        if (_teamEditLayer == null)
        {
            _teamEditLayer = UILayerLoader.Get<TeamEditLayer>();
            if (_teamEditLayer != null)
            {
                _teamEditLayer.SetTeamLegalCheck(TutorialLegal);
            }
        }
        else
        {
            if (_teamEditLayer.CurrentIsLegal())
            {
                _teamEditLayer.SetInstruction(Translate.Get("SaveYourTeam"));
            }
            else
            {
                if (_tutorialStep == "teamEdit1")
                {
                    _teamEditLayer.SetInstruction(Translate.Get("TeamEditTutorial1"));
                }
                else if (_tutorialStep == "teamEdit2")
                {
                    _teamEditLayer.SetInstruction(Translate.Get("TeamEditTutorial2"));
                }
            }
        }
        
        if (_returnLayer == null)
        {
            _returnLayer = UILayerLoader.Get<ReturnLayer>();
            if (_returnLayer != null)
                _returnLayer.gameObject.SetActive(false);
        }
    }
    
    public override void ProcessEnd()
    {
        //_teamEditLayer.SetInstruction(String.Empty);
    }
}