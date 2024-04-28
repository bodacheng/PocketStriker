using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;

public class EventFightPage : MSceneProcess
{
    private readonly EventModeManager eventModeManager;
    private EventBattleTop layer;
    
    public EventFightPage(EventModeManager eventModeManager)
    {
        this.eventModeManager = eventModeManager;
        Step = MainSceneStep.EventFight;
    }
    
    public override void ProcessEnter()
    {
        _ProcessEnter().Forget();
    }

    async UniTask _ProcessEnter()
    {
        await eventModeManager.Initialize();
        layer = UILayerLoader.Load<EventBattleTop>();
        layer.SetupCommon();
        var unit = eventModeManager.GetRepresentativeUnit();
        if (unit != null)
        {
            await layer.IconButtonFeature(unit);
        }
        
        PlayFabReadClient.GetCompletedLevels(
            (x) =>
            {
                eventModeManager.OnCloudScriptSuccess(x, layer);
                SetLoaded(true);
            }
        );
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<EventBattleTop>();
    }
}
