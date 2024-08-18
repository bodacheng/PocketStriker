using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;

public class EventFightPage : MSceneProcess
{
    private EventBattleTop layer;
    
    public EventFightPage()
    {
        Step = MainSceneStep.EventFight;
    }
    
    public override void ProcessEnter()
    {
        _ProcessEnter().Forget();
    }

    async UniTask _ProcessEnter()
    {
        await EventModeManager.Instance.Initialize();
        layer = UILayerLoader.Load<EventBattleTop>();
        layer.SetupCommon();
        var unit = EventModeManager.Instance.GetRepresentativeUnit();
        if (unit != null)
        {
            await layer.IconButtonFeature(unit);
        }
        
        PlayFabReadClient.GetCompletedLevels(
            (x) =>
            {
                EventModeManager.Instance.OnCloudScriptSuccess(x, layer);
                LowerMainBar.Open();
                SetLoaded(true);
            }
        );
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<EventBattleTop>();
    }
}
