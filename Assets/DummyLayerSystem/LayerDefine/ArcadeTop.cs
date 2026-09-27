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
    [SerializeField] StageButton evolutionStagePrefab;
    [SerializeField] NineForShow nineForShow;
    [SerializeField] Button nextChapter;
    [SerializeField] Button lastChapter;

    private MainSceneStep step;
    List<int> _currentStages = new List<int>();
    readonly List<StageButton> _stageButtons = new List<StageButton>();
    
    int _showStagesVersion;
    int _previewVersion;
    readonly SingleThreadProcessor _previewQueue = new SingleThreadProcessor();

    bool IsActiveShowStages(int version) =>
        this != null && !IsClosing && version == _showStagesVersion && container != null;

    void ClearStageButtons()
    {
        foreach (var button in _stageButtons)
        {
            if (button == null) continue;
            button.gameObject.SetActive(false);
            Destroy(button.gameObject);
        }
        _stageButtons.Clear();
    }

    public override void OnDestroy()
    {
        _showStagesVersion++;
        _previewVersion++;
        ClearStageButtons();
        base.OnDestroy();
    }

    LoadStageDelegate LoadStageMethod;
    LoadGangbangDelegate LoadGangbangMethod;
    Action<int, bool> directToStage;
    int _maxStageNum;

    void SetupCommon()
    {
        nextChapter.onClick.RemoveListener(ShowNextStages);
        lastChapter.onClick.RemoveListener(ShowLastStages);
        jumpToNewStage.onClick.RemoveListener(ToNew);
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
    
    async UniTask IconButtonFeature(UnitInfo unitInfo, int showStagesVersion)
    {
        if (!IsActiveShowStages(showStagesVersion) || unitInfo == null) return;
        var previewVersion = ++_previewVersion;
        bool IsCurrent() => IsActiveShowStages(showStagesVersion) && previewVersion == _previewVersion;
        await _previewQueue.RunAsQueued(async () =>
        {
            if (!IsCurrent()) return;
            var unitConfig = Units.GetUnitConfig(unitInfo.r_id);
            if (unitConfig == null) return;
            ProgressLayer.Loading(string.Empty);
            BackGroundPS.target.ChangeBGByElement(unitConfig.element);
            try
            {
                await UniTask.WhenAll(
                    connector.ShowModel(unitConfig.RECORD_ID),
                    nineForShow.SkillSetInfoOfUnitOnArcadePage(unitInfo.set));
                if (!IsCurrent()) return;
                nineForShow.AddOnClickToSlots(recordId =>
                {
                    if (!IsCurrent()) return;
                    var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(recordId);
                    connector.SkillShowRunWithPrepare(skillConfig.REAL_NAME).Forget();
                });
            }
            catch (Exception) when (!IsCurrent()) { }
            finally
            {
                if (IsCurrent()) ProgressLayer.Close();
            }
        });
    }

    void ToNew()
    {
        var stages = NewStages(step == MainSceneStep.ArcadeFront ? PlayerAccountInfo.Me.arcadeProcess : PlayerAccountInfo.Me.gangbangProcess,
            step == MainSceneStep.ArcadeFront ? 3:5);
        ShowStages(stages).Forget();
    }

    void ShowNextStages()
    {
        ShowStages(NewStages( _currentStages.Count > 0 ? _currentStages.Max() + 1:0,
            step == MainSceneStep.ArcadeFront ? 3:5)).Forget();
    }
    
    void ShowLastStages()
    {
        ShowStages(NewStages(_currentStages.Count > 0 ?_currentStages.Min() - 2:0,
            step == MainSceneStep.ArcadeFront ? 3:5)).Forget();
    }
    
    public async UniTask ShowStages(List<int> stages)
    {
        if (this == null || container == null || IsClosing) return;
        var version = ++_showStagesVersion;
        _previewVersion++;
        ProgressLayer.Loading("Loading stages");
        container.gameObject.SetActive(false);
        ClearStageButtons();
        _currentStages = stages != null ? new List<int>(stages) : new List<int>();
        var arcade = step == MainSceneStep.ArcadeFront;
        var progress = arcade ? PlayerAccountInfo.Me.arcadeProcess : PlayerAccountInfo.Me.gangbangProcess;
        var lastStage = _currentStages.Count > 0 ? _currentStages.Max() : 0;
        try
        {
            var tasks = new List<UniTask>(_currentStages.Count);
            foreach (var stageNo in _currentStages)
                tasks.Add(LoadStage(stageNo, stageNo == lastStage, arcade, version));
            await UniTask.WhenAll(tasks);
            if (!IsActiveShowStages(version)) return;
            Refresh(progress, arcade ? PlayFabReadClient.StageAwards : PlayFabReadClient.GangbangAwards, arcade ? 3 : 5);
            container.gameObject.SetActive(true);
        }
        finally
        {
            if (IsActiveShowStages(version)) ProgressLayer.Close();
        }
    }

    async UniTask LoadStage(int stageNo, bool clickBoss, bool arcade, int version)
    {
        var one = arcade ? await LoadStageMethod(stageNo) : await LoadGangbangMethod(stageNo);
        try
        {
            if (!IsActiveShowStages(version) || one == null) return;
            // Cards retain plain enemy snapshots. Opening a fight loads its own
            // stage instance, independent of list previews and later page changes.
            var stageBtn = Instantiate(arcade && one.EvolutionMode ? evolutionStagePrefab : normalStagePrefab,
                container.transform);
            _stageButtons.Add(stageBtn);
            stageBtn.Button.onClick.AddListener(() =>
            {
                if (IsActiveShowStages(version)) directToStage(stageNo, false);
            });
            stageBtn.name = "Stage" + stageNo;
            stageBtn.StageNo = stageNo;
            if (arcade)
                stageBtn.SetFightMode(one.ArcadeFightMode);
            stageBtn.CriticalGaugeMode = one.EvolutionMode ? CriticalGaugeMode.Normal : one.team2CGMode;
            if (one.FightMembers == null) return;
            var enemies = one.FightMembers.EnemySets.GetValues()
                .Where(unit => unit != null).Select(unit => unit.DeepCopy()).ToList();
            if (one is GangbangInfo gb)
            {
                stageBtn.LoadUnitIconsGangbang(enemies,
                    id => gb.GetTeam2GroupSet(id).Count,
                    info => IconButtonFeature(info, version), clickBoss,
                    () => IsActiveShowStages(version));
            }
            else
            {
                stageBtn.LoadUnitIcons(enemies,
                    info => IconButtonFeature(info, version), clickBoss,
                    () => IsActiveShowStages(version));
            }
        }
        finally
        {
            // Only adventure loads return owned clones. Gangbang's legacy loader
            // still returns a shared asset, and active battle data is never ours.
            if (arcade && one != null && !ReferenceEquals(one, FightLoad.Fight))
                Destroy(one);
        }
    }

    void Refresh(int progress, IDictionary<string, Award> stageAwards, int stageCountPerPage)
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
        lastChapter.gameObject.SetActive(_currentStages.Count == 0 || _currentStages.Min() > stageCountPerPage);

        var progressChapter = progress == _maxStageNum
            ? (progress - 1) / stageCountPerPage
            : progress / stageCountPerPage;
        var currentChapter = _currentStages.Count != 0 ? _currentStages.Min() / stageCountPerPage : _maxStageNum / stageCountPerPage;
        
        jumpToNewStage.gameObject.SetActive(progressChapter != currentChapter);
        
        container.CalculateLayoutInputHorizontal();
        container.CalculateLayoutInputVertical();
        container.SetLayoutHorizontal();
        container.SetLayoutVertical();
    }
    
    public List<int> NewStages(int progress, int stageCountPerPage)
    {
        if (progress > _maxStageNum)
        {
            progress = _maxStageNum - 1;
        }
        else if (progress == _maxStageNum)
        {
            progress -= 1;
        }
        
        var currentChapter = progress / stageCountPerPage;
        var returnValue = new List<int>();
        for (int stageNoPlus = 1; stageNoPlus <= stageCountPerPage; stageNoPlus++)
        {
            int targetNo = stageNoPlus + currentChapter * stageCountPerPage;
            if (targetNo <= _maxStageNum)
            {
                returnValue.Add(targetNo);
            }
        }
        return returnValue;
    }
}
