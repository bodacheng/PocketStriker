using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using mainMenu;
using dataAccess;
using DummyLayerSystem;
using ModelView;
using UnityEngine.UI;

public class TeamSingleSelectLayer : UILayer
{
    [SerializeField] DedicatedCameraConnector connector;
    [SerializeField] Image view2D;
    [SerializeField] Animator unitOutAnimator;
    
    [Header("选中框")] [SerializeField] GameObject selectedFrame;

    [Header("选中角色的技能显示")] [SerializeField] NineForShow nineForShow;

    [Header("队伍保存")] [SerializeField] BOButton saveBtn;
    
    [Header("技能编辑按钮")]
    [SerializeField] BOButton skillEditButton;
    [SerializeField] ConfirmBtnColorSwapper skillEditBtnColorSwapper;

    [Header("指示")] [SerializeField] Text instruction;
    
    private Func<string, bool> _teamLegal;
    private string _currentTeamMode;
    
    public void Ini(string teamMode, Action save, Func<string, bool> teamLegal, bool isTutorial)
    {
        _currentTeamMode = teamMode;
        SetTeamLegalCheck(teamLegal);
        
        void SkillEdit()
        {
            if (PreScene.target.Focusing != null && PreScene.target.Focusing.id != null)
                PreScene.target.trySwitchToStep(MainSceneStep.UnitSkillEdit);
        }
        if (!isTutorial)
            skillEditButton.onClick.AddListener(SkillEdit);
        
        saveBtn.onClick.AddListener(()=> save());
    }
    
    public void SetTeamLegalCheck(Func<string, bool> teamLegal)
    {
        _teamLegal = teamLegal;
        SetConfirmBtnActive();
    }
    
    /// <summary>
    /// Change target pos unit
    /// </summary>
    /// <param name="instanceID"></param>
    /// <param name="targetPos"></param>
    /// <param name="teamMode"></param>
    public void ChangeTeamPos(string instanceID, int targetPos, string teamMode)
    {
        var unitsLayer = UILayerLoader.Get<UnitsLayer>();
        var unitInfo = dataAccess.Units.Get(instanceID);
        if (unitInfo == null) return;
        
        PreScene.target.SetFocusingUnit(instanceID);
        nineForShow.ShowStones_Acc(instanceID);
        
        UniTask.WhenAll(
            connector.ShowMyModel(instanceID), 
            Set2DView(unitInfo.r_id, view2D, unitOutAnimator,
                0, 0.6f, 0, DedicatedCameraConnector.Unit2DViewYoKoSpaceWhenAtRight(unitInfo.r_id))).Forget();
        
        unitsLayer.Selected.Value = instanceID;
        
        TeamSet.GetTargetSet(teamMode).SetPosUnitByInstanceID(targetPos, instanceID);
        SetConfirmBtnActive();
    }
    
    private void SetConfirmBtnActive()
    {
        bool legal = _teamLegal(_currentTeamMode);
        skillEditBtnColorSwapper.ChangeColor(legal ? Color.green : new Color(1,1,1,0.5f));
        saveBtn.interactable = legal;
    }
}
