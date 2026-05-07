using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine;
using dataAccess;

public partial class StoneListLayer : UILayer
{
    public SkillStonesBox box;
    public SSLevelUpManager levelManager;
    [SerializeField] SkillStoneDetail skillStoneDetail;
    [SerializeField] BOButton openPowerUpBtn;

    public SkillStoneDetail SkillStoneDetail => skillStoneDetail;

    string _targetStoneID;
    public string TargetStoneID
    {
        get => _targetStoneID;
        set
        {
            _targetStoneID = value;
            var info = Stones.Get(_targetStoneID);
            if (info != null)
            {
                skillStoneDetail.RefreshInfo(_targetStoneID);
            }
            else
            {
                skillStoneDetail.Clear();
            }
            skillStoneDetail.gameObject.SetActive(info != null);
            openPowerUpBtn.gameObject.SetActive(Stones.StoneCanLevelUp(_targetStoneID));
            if (info == null) return;
            openPowerUpBtn.SetListener(
                () =>
                {
                    OnBeforeOpenPowerUpPage();
                    levelManager.OpenLevelUpPage();
                }
            );
        }
    }

    void OnEnable()
    {
        ToggleTabListener(true);
        SKStoneItem.DragBlocked = true;
    }

    void OnDisable()
    {
        ToggleTabListener(false);
        SKStoneItem.DragBlocked = false;
    }

    public async UniTask Setup()
    {
        var cts = new CancellationTokenSource();
        ReturnLayer.AddUniTaskCancel(cts);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, gameObject.GetCancellationTokenOnDestroy());
        try
        {
            OnBeforeSetup();
            Stones.ClearTempUnitUsage();
            box.IniExTabs();
            box.GenerateCells();
            await box._tabEffects.SwitchElement(Element.blueMagic, linkedCts.Token);
            await box.IniExTabsEffects(PreScene.target.noPostProcessCamera, linkedCts.Token);
            linkedCts.Token.ThrowIfCancellationRequested();
            box.AddFeatureToCells(CellFeature_StoneShow);
            box.FilterFeatureRefresh(true);
            box.RestFilter();
            skillStoneDetail.Clear();
            levelManager.INI();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            linkedCts.Dispose();
        }
    }

    public override void OnDestroy()
    {
        skillStoneDetail.Clear();
        if (box != null)
        {
            ToggleTabListener(false);
            box._tabEffects.CloseShowingTagEffects();
        }
        base.OnDestroy();
    }

    public void CellFeature_StoneShow(StoneCell cell)
    {
        void BtnFeature()
        {
            var stone = cell.GetItem();
            if (stone != null && stone._SkillConfig != null)
            {
                StoneCell.SelectedRender(cell, SkillStonesBox.Selected);
                TargetStoneID = stone.instanceId;
            }else{
                TargetStoneID = null;
            }
        }

        cell.btn.ActivateHold = false;
        cell.btn.ActivateDoubleClick = false;

        cell.btn.SetListener(BtnFeature);
        cell.SetOnDropAction(StoneCell.Install);
    }

    public void CellFeature_MAdd(StoneCell cell)
    {
        void BtnFeature()
        {
            StoneCell.SelectedRender(cell, SkillStonesBox.Selected);
        }
        void DoubleClick()
        {
            levelManager.AddMaterialFromCell(cell);
        }

        cell.btn.SetListener(BtnFeature);
        cell.btn.ActivateDoubleClick = true;
        cell.btn.onDoubleClick.AddListener(DoubleClick);
        cell.SetOnDropAction(StoneCell.Install);
    }

    void ClearSelectionFromTabs()
    {
        if (SkillStonesBox.Selected != null)
        {
            StoneCell.SelectedRender(null, SkillStonesBox.Selected);
        }
        TargetStoneID = null;
    }

    void ToggleTabListener(bool subscribe)
    {
        if (box == null)
            return;
        if (subscribe)
        {
            box.ExTabPressed -= ClearSelectionFromTabs;
            box.ExTabPressed += ClearSelectionFromTabs;
        }
        else
        {
            box.ExTabPressed -= ClearSelectionFromTabs;
        }
    }

    partial void OnBeforeSetup();
    partial void OnBeforeOpenPowerUpPage();
}
