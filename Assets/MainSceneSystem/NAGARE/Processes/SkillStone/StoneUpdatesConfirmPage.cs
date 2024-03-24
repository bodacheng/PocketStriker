using DummyLayerSystem;
using mainMenu;

public class StoneUpdatesConfirmPage : MSceneProcess
{
    public StoneUpdatesConfirmPage()
    {
        Step = MainSceneStep.StoneUpdateConfirm;
    }
    
    private StoneUpdatesConfirm layer;
    
    public override void ProcessEnter()
    {
        layer = UILayerLoader.Load<StoneUpdatesConfirm>();
        layer.ShowInfo(
            ExecuteUpdateAll,
            StoneLevelUpProccessor.needGoldWhole,
            StoneLevelUpProccessor.UpdateAllStoneForms
        );
        SetLoaded(true);
    }

    public override void ProcessEnd()
    {
        UILayerLoader.Remove<StoneUpdatesConfirm>();
    }
    
    void ExecuteUpdateAll()
    {
        if (Currencies.CoinCount.Value < StoneLevelUpProccessor.needGoldWhole)
        {
            PopupLayer.ArrangeWarnWindow(Translate.Get("NoEnoughGD"));
            return;
        }
        PreScene.target.trySwitchToStep(MainSceneStep.SkillStoneList, true, false);
    }
}
