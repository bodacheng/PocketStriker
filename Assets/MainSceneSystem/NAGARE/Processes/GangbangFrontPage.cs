using Cysharp.Threading.Tasks;
using DummyLayerSystem;
using mainMenu;

public delegate UniTask<GangbangInfo> LoadGangbangDelegate(int stageNo);

public class GangbangFrontPage : MSceneProcess
{
    readonly GangbangModeManager _gangbangModeManager;
    ArcadeTop _arcadeTop;
    
    public GangbangFrontPage(GangbangModeManager gangbangModeManager)
    {
        Step = MainSceneStep.GangBangFront;
        this._gangbangModeManager = gangbangModeManager;
    }
    
    public override void ProcessEnter()
    {
        _arcadeTop = UILayerLoader.Load<ArcadeTop>();
        Load().Forget();
    }
    
    async UniTask Load()
    {
        await _gangbangModeManager.Initialize();
        _arcadeTop.SetupGangbangArcade(_gangbangModeManager.MaxStageNum, _gangbangModeManager.LoadStage, _gangbangModeManager.DirectToGangStage);
        var stages = _arcadeTop.NewStages(PlayerAccountInfo.Me.gangbangProcess);
        await _arcadeTop.ShowStages(stages);
        SetLoaded(true);
    }
    
    public override void ProcessEnd()
    {
        UILayerLoader.Remove<ArcadeTop>();
    }
}
