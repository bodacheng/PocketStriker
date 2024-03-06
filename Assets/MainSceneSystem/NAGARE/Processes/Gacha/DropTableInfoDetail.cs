using DummyLayerSystem;
using mainMenu;

public class DropTableInfoDetail : MSceneProcess
{
    private DropTableInfoLayer _layer;
    private string tableId;
    
    public DropTableInfoDetail()
    {
        Step = MainSceneStep.DropTableInfo;
    }

    public override void ProcessEnter<T>(T tableId)
    {
        BackGroundPS.target.ChangeBGByElement(Element.lightMagic);
        _layer = UILayerLoader.Load<DropTableInfoLayer>();
        this.tableId = tableId as string;
        CloudScript.GetDropTableInfo(_layer.ShowDropTableInfo, this.tableId);
        UILayerLoader.Remove<UpperInfoBar>();
    }
    
    public override void ProcessEnter()
    {
        ProcessEnter(tableId);
    }

    public override void ProcessEnd()
    {
        UILayerLoader.Remove<DropTableInfoLayer>();
    }
    
    public override bool CanEnterOtherProcess()
    {
        return true;
    }
}
