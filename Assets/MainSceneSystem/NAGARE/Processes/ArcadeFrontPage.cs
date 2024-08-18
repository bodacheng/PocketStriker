using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;

public delegate UniTask<FightInfo> LoadStageDelegate(int stageNo);
public class ArcadeFrontPage : MSceneProcess
{
    ArcadeTop _arcadeTop;
    
    public ArcadeFrontPage()
    {
        Step = MainSceneStep.ArcadeFront;
    }
    
    public override void ProcessEnter()
    {
        _arcadeTop = UILayerLoader.Load<ArcadeTop>();
        Load().Forget();
    }
    
    async UniTask Load()
    {
        await ArcadeModeManager.Instance.Initialize();
        _arcadeTop.SetupArcade(ArcadeModeManager.Instance.MaxStageNum, ArcadeModeManager.Instance.LoadStage, ArcadeModeManager.Instance.DirectToArcadeStage);
        var stages = _arcadeTop.NewStages(PlayerAccountInfo.Me.arcadeProcess, 3);
        await _arcadeTop.ShowStages(stages);
        LowerMainBar.Open();
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<ArcadeTop>();
    }
}

public class StageAward
{
    public string stageKey;
    public Award award;
}

public class Award
{
    public int g;
    public int d;
}