using DummyLayerSystem;
using mainMenu;

public class OpenSkillEdit : TutorialProcess
{
    ReturnLayer _returnLayer;
    UnitListPage _unitListPage;
    UnitsLayer _unitsLayer;
    UnitOptionLayer _unitOptionLayer;
    LowerMainBar _lowerMainBar;
    UpperInfoBar _upperInfoBar;

    UnitInfo _focusUnitInfo;
    readonly string _focusUnitRId;
    
    public OpenSkillEdit(string unitRId)
    {
        _focusUnitRId = unitRId;
    }

    public override void ProcessEnter()
    {
        _focusUnitInfo = dataAccess.Units.GetByRId(_focusUnitRId);
        _unitListPage = (UnitListPage)ProcessesRunner.Main.GetProcess(MainSceneStep.UnitList);
    }
    
    public override void ProcessEnd()
    {
        HighLightLayer.Close();
    }
    
    public override bool CanEnterOtherProcess()
    {
        return ProcessesRunner.Main.currentProcess.Step == MainSceneStep.UnitSkillEdit;
    }
    
    public override void LocalUpdate()
    {
        if (_returnLayer == null)
        {
            _returnLayer = UILayerLoader.Get<ReturnLayer>();
        }
        if (_returnLayer != null)
        {
            _returnLayer.gameObject.SetActive(false);
        }
        
        if (_lowerMainBar == null)
        {
            _lowerMainBar = UILayerLoader.Get<LowerMainBar>();
            if (_lowerMainBar != null)
            {
                _lowerMainBar.PlsClickBtn(MainSceneStep.None);
            }
        }
        
        if (_upperInfoBar == null)
        {
            _upperInfoBar = UILayerLoader.Get<UpperInfoBar>();
            if (_upperInfoBar != null)
            {
                _upperInfoBar.Interactable(false);
            }
        }
        
        if (_unitOptionLayer == null)
            _unitOptionLayer = UILayerLoader.Get<UnitOptionLayer>();
        
        if (_unitListPage.GetLoaded() && _unitOptionLayer != null)
        {
            _unitOptionLayer.PlsClickSkillEdit();
        }
        
        if (_unitsLayer == null)
        {
            _unitsLayer = UILayerLoader.Get<UnitsLayer>();
        }
        else
        {
            if (_focusUnitInfo != null && _unitsLayer.Selected.Value != _focusUnitInfo.id)
            {
                _unitsLayer.OnClick(_focusUnitInfo.id);
            }
        }
    }
}