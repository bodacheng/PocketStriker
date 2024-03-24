using System.Threading;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine;
using dataAccess;

public class StoneListLayer : UILayer
{
    public SkillStonesBox box;
    public SSLevelUpManager levelManager;
    [SerializeField] SkillStoneDetail skillStoneDetail;
    [SerializeField] BOButton openPowerUpBtn;
    
    public SkillStoneDetail SkillStoneDetail=> skillStoneDetail;
    
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
            skillStoneDetail.gameObject.SetActive(info != null);
            openPowerUpBtn.gameObject.SetActive(Stones.StoneCanLevelUp(_targetStoneID));
            if (info == null) return;
            openPowerUpBtn.SetListener(
                () =>
                {
                    skillStoneDetail.SkillIntro.gameObject.SetActive(false);
                    levelManager.OpenLevelUpPage();
                }
            );
        }
    }
    
    public async void Setup()
    {
        var cts = new CancellationTokenSource();
        ReturnLayer.AddUniTaskCancel(cts);
        
        box.IniExTabs();
        box.GenerateCells();
        await box._tabEffects.SwitchElement(Element.blueMagic, cts.Token);
        await box.IniExTabsEffects(PreScene.target.postProcessCamera, cts.Token);
        box.AddFeatureToCells(CellFeature_StoneShow);
        box.FilterFeatureRefresh(true);
        box.RestFilter();
        skillStoneDetail.Clear();
        levelManager.INI();
    }
    
    public override void OnDestroy()
    {
        skillStoneDetail.Clear();
        box._tabEffects.CloseShowingTagEffects();
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
                skillStoneDetail.Clear();
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
}