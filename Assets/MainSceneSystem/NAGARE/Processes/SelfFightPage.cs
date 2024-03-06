using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;

public class SelfFightPage : MSceneProcess
{
    public SelfFightPage()
    {
        Step = MainSceneStep.SelfFightFront;
    }

    SelfFightLayer selfFightLayer;

    public override void ProcessEnter()
    {
        SetUp().Forget();
    }

    async UniTask SetUp()
    {
        var layer = UILayerLoader.Load<UnitsLayer>();
        layer.DisplayUnitIcons(dataAccess.Units.Dic, true, true);
        
        selfFightLayer = UILayerLoader.Load<SelfFightLayer>();
        await selfFightLayer.INI();
        selfFightLayer.AddUnitIconFeaturesToBox();
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<UnitsLayer>();
        UILayerLoader.Remove<SelfFightLayer>();
    }
}
