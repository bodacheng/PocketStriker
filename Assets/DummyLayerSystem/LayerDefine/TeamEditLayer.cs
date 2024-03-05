using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using mainMenu;
using dataAccess;
using DummyLayerSystem;
using ModelView;
using UnityEngine.UI;
using UniRx;

public class TeamEditLayer : UILayer
{
    [SerializeField] DedicatedCameraConnector connector;
    [SerializeField] Image view2D;
    [SerializeField] Animator unitOutAnimator;
    [SerializeField] Button removeButton;
    [SerializeField] HeroCell team1Front, team1Left, team1Right;
    
    [Header("角色属性框")]
    [SerializeField] HeroIcon noMagic;
    
    [Header("选中框")]
    [SerializeField] GameObject selectedFrame;
    
    [Header("选中角色的技能显示")]
    [SerializeField] NineForShow nineForShow;
    
    [Header("队伍保存")]
    [SerializeField] BOButton saveBtn;
    
    [Header("技能编辑按钮")]
    [SerializeField] BOButton skillEditButton;
    [SerializeField] ConfirmBtnColorSwapper skillEditBtnColorSwapper;

    [Header("指示")]
    [SerializeField] Text instruction;
    
    readonly ReactiveProperty<int> _focusingPos = new ReactiveProperty<int>(-1);
    readonly IDictionary<int, HeroCell> _teamBtnDic = new Dictionary<int, HeroCell>();
    private Func<string, bool> _teamLegal;
    private string _currentTeamMode;

    public void SetInstruction(string value)
    {
        instruction.text = value;
    }
    
    public void SetTeamLegalCheck(Func<string, bool> teamLegal)
    {
        _teamLegal = teamLegal;
        SetConfirmBtnActive();
    }

    public bool CurrentIsLegal()
    {
        return _teamLegal != null && _teamLegal(_currentTeamMode);
    }

    private void SetConfirmBtnActive()
    {
        bool legal = _teamLegal(_currentTeamMode);
        skillEditBtnColorSwapper.ChangeColor(legal ? Color.green : new Color(1,1,1,0.5f));
        saveBtn.interactable = legal;
    }
    
    /// <summary>
    /// unit box icon feature
    /// </summary>
    /// <param name="instanceID"></param>
    /// <param name="teamMode"></param>
    public void UnitIconClick(string instanceID, string teamMode)
    {
        var unitsLayer = UILayerLoader.Get<UnitsLayer>();
        if (unitsLayer == null) return;
        if (_focusingPos.Value != -1)
        {
            ChangeTeamPos(instanceID, _focusingPos.Value, teamMode);
            unitsLayer.Selected.Value = null;
        }
        else
        {
            unitsLayer.Selected.Value = instanceID;
        }
        
        PreScene.target.SetFocusingUnit(instanceID);
        nineForShow.ShowStones_Acc(instanceID);
        
        var unitInfo = dataAccess.Units.Get(instanceID);
        if (unitInfo != null)
        {
            UniTask.WhenAll(
                connector.ShowMyModel(instanceID), 
                Set2DView(unitInfo.r_id, view2D, unitOutAnimator,
                    0, 0.6f, 0, DedicatedCameraConnector.Unit2DViewYoKoSpaceWhenAtRight(unitInfo.r_id))).Forget();
        }
        else
        {
            connector.ShowMyModel(instanceID).Forget();
        }
    }
    
    /// <summary>
    /// Change target pos unit
    /// </summary>
    /// <param name="instanceID"></param>
    /// <param name="targetPos"></param>
    /// <param name="teamMode"></param>
    void ChangeTeamPos(string instanceID, int targetPos, string teamMode)
    {
        var unitInfo = dataAccess.Units.Get(instanceID);
        if (unitInfo != null && Stones.GetEquippingStones(instanceID).Count != 9)
        {
            _focusingPos.Value = -1;
            return;
        }
        
        var returns = TeamSet.GetTargetSet(teamMode).SetPosUnitByInstanceID(targetPos, instanceID);
        foreach (var t in returns)
        {
            ChangeIconOnPos(t.posNum, teamMode);
        }

        if (returns.Count > 0)
        {
            _focusingPos.Value = -1;
        }
        
        SetConfirmBtnActive();
    }
    
    // 纯渲染函数
    void ChangeIconOnPos(int posNum, string teamMode)
    {
        var cell = _teamBtnDic[posNum];
        var posInstanceID = TeamSet.GetTargetSet(teamMode).GetInstanceIdOnPos(posNum);
        var info = dataAccess.Units.Get(posInstanceID);
        if (info != null)
        {
            var targetingIcon = Instantiate(noMagic);
            targetingIcon.name = info.id + "_icon";
            targetingIcon.InstanceID = posInstanceID;
            targetingIcon.ChangeIcon(info);
            targetingIcon.iconButton.SetListener(() =>
            {
                SetPos(posNum, teamMode);
            });
            cell.AddItem(targetingIcon);
        }
        else
        {
            cell.Clear();
        }
    }
    
    public void Ini(string teamMode, Action save, Func<string, bool> teamLegal, bool isTutorial)
    {
        _currentTeamMode = teamMode;
        SetTeamLegalCheck(teamLegal);
        _focusingPos.Subscribe((x) =>
        {
            var posInstanceID = TeamSet.GetTargetSet(teamMode).GetInstanceIdOnPos(x);
            removeButton.gameObject.SetActive(x != -1 && posInstanceID != null);
            switch (x)
            {
                case 0:
                    HeroIcon.SelectedFeature(team1Front.transform, selectedFrame, 1f);
                    break;
                case 1:
                    HeroIcon.SelectedFeature(team1Left.transform, selectedFrame, 1f);
                    break;
                case 2:
                    HeroIcon.SelectedFeature(team1Right.transform, selectedFrame, 1f);
                    break;
                default:
                    HeroIcon.SelectedFeature(null, selectedFrame, 1f);
                    break;
            }
        }).AddTo(gameObject);
        
        team1Front.TeamEdit = (s) => { ChangeTeamPos(s,0,teamMode); };
        team1Left.TeamEdit = (s) => { ChangeTeamPos(s,1,teamMode); };
        team1Right.TeamEdit = (s) => { ChangeTeamPos(s,2,teamMode); };
        
        _teamBtnDic.Clear();
        _teamBtnDic.Add(0, team1Front);
        _teamBtnDic.Add(1, team1Left);
        _teamBtnDic.Add(2, team1Right);
        
        // 适配队伍编辑器各个位置初始头像
        ChangeIconOnPos(0, teamMode);
        ChangeIconOnPos(1, teamMode);
        ChangeIconOnPos(2, teamMode);
        
        void SkillEdit()
        {
            if (PreScene.target.Focusing != null && PreScene.target.Focusing.id != null)
                PreScene.target.trySwitchToStep(MainSceneStep.UnitSkillEdit);
        }
        if (!isTutorial)
            skillEditButton.onClick.AddListener(SkillEdit);
        
        removeButton.onClick.AddListener(Remove);
        team1Front.iconButton.onClick.AddListener(() => SetPos(0, teamMode));
        team1Left.iconButton.onClick.AddListener(() => SetPos(1, teamMode));
        team1Right.iconButton.onClick.AddListener(() => SetPos(2, teamMode));
        saveBtn.onClick.AddListener(()=>save());
    }
    
    void Remove()
    {
        ChangeTeamPos(null, _focusingPos.Value, _currentTeamMode);
    }
    
    void SetPos(int posNum, string teamMode)
    {
        var unitsLayer = UILayerLoader.Get<UnitsLayer>();
        if (unitsLayer == null) return;
        var selectedInstanceID = unitsLayer.Selected.Value;
        if (selectedInstanceID != null)
        {
            Remove();
            unitsLayer.Selected.Value = null;
            ChangeTeamPos(selectedInstanceID, posNum, teamMode);
        }
        else
        {
            _focusingPos.Value = posNum;
            var instanceID = TeamSet.GetTargetSet(teamMode).GetInstanceIdOnPos(_focusingPos.Value);
            PreScene.target.SetFocusingUnit(instanceID);
                
            var unitInfo = dataAccess.Units.Get(instanceID);
            if (unitInfo != null)
            {
                UniTask.WhenAll(
                    connector.ShowMyModel(instanceID), 
                    Set2DView(unitInfo.r_id, view2D, unitOutAnimator,
                        0, 0.6f, 0, DedicatedCameraConnector.Unit2DViewYoKoSpaceWhenAtRight(unitInfo.r_id))).Forget();
            }
            else
            {
                connector.ShowMyModel(instanceID).Forget();
            }
                
            if (PreScene.target.Focusing != null)
                nineForShow.ShowStones_Acc(PreScene.target.Focusing.id);
            else
            {
                nineForShow.ShowStones_Acc(null);
            };
        }
    }
}