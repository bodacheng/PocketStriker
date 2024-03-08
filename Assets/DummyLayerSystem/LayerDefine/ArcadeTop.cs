using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Cysharp.Threading.Tasks;
using ModelView;
using mainMenu;

public class ArcadeTop : UILayer
{
    [SerializeField] DedicatedCameraConnector connector;
    [SerializeField] VerticalLayoutGroup container;
    [SerializeField] Button jumpToNewStage;
    [SerializeField] StageButton normalStagePrefab;
    [SerializeField] StageButton bossStagePrefab;
    [SerializeField] NineForShow nineForShow;
    [SerializeField] Button nextChapter;
    [SerializeField] Button lastChapter;

    private MainSceneStep step;
    List<int> _currentStages;
    readonly List<StageButton> _stageButtons = new List<StageButton>();
    
    LoadStageDelegate LoadStageMethod;
    LoadGangbangDelegate LoadGangbangMethod;
    Action<int, bool> directToStage;
    int _maxStageNum;

    void SetupCommon()
    {
        nextChapter.onClick.AddListener(ShowNextStages);
        lastChapter.onClick.AddListener(ShowLastStages);
        jumpToNewStage.onClick.AddListener(ToNew);
        
        var camRect = connector.GetComponent<RectTransform>();
        ResizeCameraConnectorAsMaxSquare(camRect, camRect.rect.width, camRect.rect.height);
    }
    
    public void SetupArcade(int maxStageNum, LoadStageDelegate loadFightInfo, Action<int, bool> directToStage)
    {
        step = MainSceneStep.ArcadeFront;
        this.LoadStageMethod = loadFightInfo;
        this.directToStage = directToStage;
        this._maxStageNum = maxStageNum;
        SetupCommon();
    }
    
    public void SetupGangbangArcade(int maxStageNum, LoadGangbangDelegate loadFightInfo, Action<int, bool> directToStage)
    {
        step = MainSceneStep.GangBangFront;
        this.LoadGangbangMethod = loadFightInfo;
        this.directToStage = directToStage;
        this._maxStageNum = maxStageNum;
        SetupCommon();
    }
    
    async UniTask IconButtonFeature(UnitInfo unitInfo)
    {
        UnitConfig unitConfig = Units.GetUnitConfig(unitInfo.r_id);
        
        ProgressLayer.Loading(string.Empty);
        BackGroundPS.target.ChangeBGByElement(unitConfig.element);
        
        await UniTask.WhenAll(
            connector.ShowModel(unitConfig.RECORD_ID), 
            nineForShow.SkillSetInfoOfUnitOnArcadePage(unitInfo.set)
        );
        
        nineForShow.AddOnClickToSlots(
            (RECORD_ID) =>
            {
                var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(RECORD_ID);
                connector.SkillShowRunWithPrepare(skillConfig.REAL_NAME).Forget();
            }
        );
        ProgressLayer.Close();
    }

    void ToNew()
    {
        var stages = NewStages(step == MainSceneStep.ArcadeFront ? PlayerAccountInfo.Me.arcadeProcess : PlayerAccountInfo.Me.gangbangProcess);
        ShowStages(stages).Forget();
    }

    void ShowNextStages()
    {
        ShowStages(NewStages( _currentStages.Count > 0 ? _currentStages.Max() + 1:0)).Forget();
    }
    
    void ShowLastStages()
    {
        ShowStages(NewStages(_currentStages.Count > 0 ?_currentStages.Min() - 2:0)).Forget();
    }
    
    public async UniTask ShowStages(List<int> stages)
    {
        ProgressLayer.Loading("Loading stages");
        container.transform.gameObject.SetActive(false);
        foreach (var child in _stageButtons) {
            Destroy(child.gameObject);
        }
        _stageButtons.Clear();
        _currentStages = stages;
        var tasks = new List<UniTask>();
        for (var index = 0; index < _currentStages.Count; index++)
        {
            tasks.Add(LoadStage(_currentStages[index]));
        }
        await UniTask.WhenAll(tasks);
        Refresh(step == MainSceneStep.ArcadeFront ? PlayerAccountInfo.Me.arcadeProcess : PlayerAccountInfo.Me.gangbangProcess,  
            step == MainSceneStep.ArcadeFront ? PlayFabReadClient.StageAwards : PlayFabReadClient.GangbangAwards);
        if (container != null)
            container.transform.gameObject.SetActive(true);
        ProgressLayer.Close();
    }
    
    async UniTask LoadStage(int stageNo)
    {
        var one = step == MainSceneStep.ArcadeFront ? await LoadStageMethod(stageNo) : await LoadGangbangMethod(stageNo);
        if (one == null)
        {
            return;
        }
        
        var stageBtn = Instantiate(stageNo % 5 == 0 ? bossStagePrefab : normalStagePrefab);
        _stageButtons.Add(stageBtn);
        stageBtn.Button.onClick.AddListener(
            ()=>
            {
                directToStage(stageNo, false);
            }
        );
        stageBtn.name = "Stage" + stageNo;
        stageBtn.StageNo = stageNo;
        stageBtn.CriticalGaugeMode = one.team2CGMode;
        if (one.FightMembers != null)
        {
            if (one is GangbangInfo)
            {
                var gb = (GangbangInfo)one;
                stageBtn.LoadUnitIconsGangbang(
                    one.FightMembers.EnemySets.GetValues(), 
                    (x)=> gb.GetTeam2GroupSet(x).Count,
                    IconButtonFeature, 
                    stageNo == _currentStages.Max());
            }
            else
            {
                stageBtn.LoadUnitIcons(one.FightMembers.EnemySets.GetValues(), IconButtonFeature, stageNo == _currentStages.Max());
            }
            
        }
    }

    void Refresh(int progress, IDictionary<string, Award> stageAwards)
    {
        if (container.IsDestroyed())
            return;
        
        _stageButtons.Sort((a, b) => b.StageNo.CompareTo(a.StageNo));
        for (var i = 0; i < _stageButtons.Count; i++)
        {
            var stageBtn = _stageButtons[i];
            var btnAnimator = stageBtn.GetComponent<Animator>();
            if (btnAnimator != null)
                btnAnimator.enabled = progress + 1 == stageBtn.StageNo;
            
            var rewardDic = stageAwards;
            var reward = rewardDic[stageBtn.StageNo.ToString()];
            stageBtn.RewardUI.ShowRewards(reward.d,reward.g);
            stageBtn.RewardUI.AwardRender(progress + 1 > stageBtn.StageNo);
            stageBtn.ChangeColorOfIcons(progress + 1 >= stageBtn.StageNo);
            stageBtn.transform.SetParent(container.transform);
            stageBtn.transform.localPosition = Vector3.zero;
            stageBtn.transform.localRotation = Quaternion.identity;
            stageBtn.transform.localScale = Vector3.one;
        }
        
        int currentStagesMax = _currentStages.Count > 0 ? _currentStages.Max() : progress;
        nextChapter.gameObject.SetActive((progress + 1 > currentStagesMax) && (_maxStageNum > currentStagesMax));
        lastChapter.gameObject.SetActive(_currentStages.Count == 0 || _currentStages.Min() > 5);

        var progressChapter = progress == _maxStageNum
            ? (progress - 1) / 5
            : progress / 5;
        var currentChapter = _currentStages.Count != 0 ? _currentStages.Min() / 5 : _maxStageNum / 5;
        
        jumpToNewStage.gameObject.SetActive(progressChapter != currentChapter);
        
        container.CalculateLayoutInputHorizontal();
        container.CalculateLayoutInputVertical();
        container.SetLayoutHorizontal();
        container.SetLayoutVertical();
    }
    
    public List<int> NewStages(int progress)
    {
        if (progress > _maxStageNum)
        {
            progress = _maxStageNum;
        }
        else if (progress == _maxStageNum)
        {
            progress -= 1;
        }

        var currentChapter = progress / 5;
        var returnValue = new List<int>();
        for (int stageNoPlus = 1; stageNoPlus <= 5; stageNoPlus++)
        {
            int targetNo = stageNoPlus + currentChapter * 5;
            if (targetNo <= _maxStageNum)
            {
                returnValue.Add(targetNo);
            }
        }
        return returnValue;
    }
}