using DummyLayerSystem;
using mainMenu;

public class UnitListPage : MSceneProcess
{
    private UnitsLayer layer;
    private UnitOptionLayer unitOptionLayer;
    
    public UnitListPage()
    {
        Step = MainSceneStep.UnitList;
    }
    
    public override void ProcessEnter()
    {
        unitOptionLayer = UILayerLoader.Load<UnitOptionLayer>();
        layer = UILayerLoader.Load<UnitsLayer>();
        ReturnLayer.MoveFront();
        void UnitIconBtn(string instanceId)
        {
            layer.Selected.Value = instanceId;
            PreScene.target.SetFocusingUnit(instanceId);
            unitOptionLayer.RefreshMemberDetailPageByFocusingUnit();
        }
        layer.SetDisplayUnitIconsAfterAction(
            () =>
            {
                layer.SetUnitsIconOnClick(UnitIconBtn);
            }
        );
        layer.DisplayUnitIcons(dataAccess.Units.Dic, true);
        UnitIconBtn(PreScene.target.Focusing.id);
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<UnitOptionLayer>();
        UILayerLoader.Remove<UnitsLayer>();
    }
}
