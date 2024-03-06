using DummyLayerSystem;
using mainMenu;

public class ArenaAwardPage : MSceneProcess
{
    private ArenaAwardLayer layer;
    public ArenaAwardPage()
    {
        Step = MainSceneStep.ArenaAward;
    }
    
    public override void ProcessEnter()
    {
        layer = UILayerLoader.Load<ArenaAwardLayer>();
        layer.SetUp(PlayFabReadClient.ArenaAwards);
    }
    
    public override bool CanEnterOtherProcess()
    {
        return true;
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<ArenaAwardLayer>();
    }
}
