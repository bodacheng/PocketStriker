using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;

public delegate UniTask<GangbangInfo> LoadGangbangDelegate(int stageNo);

public class GangbangFrontPage : MSceneProcess
{
    ArcadeTop _arcadeTop;
    
    public GangbangFrontPage()
    {
        Step = MainSceneStep.GangBangFront;
    }
    
    public override void ProcessEnter()
    {
        _arcadeTop = UILayerLoader.Load<ArcadeTop>();
        Load().Forget();
    }
    
    async UniTask Load()
    {
        await GangbangModeManager.Instance.Initialize();
        _arcadeTop.SetupGangbangArcade(GangbangModeManager.Instance.MaxStageNum, GangbangModeManager.Instance.LoadStage, GangbangModeManager.Instance.DirectToGangStage);
        var stages = _arcadeTop.NewStages(PlayerAccountInfo.Me.gangbangProcess, 5);
        await _arcadeTop.ShowStages(stages);
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<ArcadeTop>();
    }
}
