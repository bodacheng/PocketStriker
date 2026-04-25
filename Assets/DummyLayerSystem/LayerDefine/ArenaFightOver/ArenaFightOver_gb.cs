using DummyLayerSystem;

public partial class ArenaFightOver : UILayer
{
    public async void LoadNextGangbangStage(int stageNo)
    {
        var nextFight = await GangbangModeManager.Instance.LoadStage(stageNo);
        if (nextFight != null)
        {
            nextTab.SetUp(-1, "Stage " + stageNo);
            nextTab.gameObject.SetActive(true);
            nextTab.SetUpAction(() =>
            {
                var newFightInstance = GangbangInfo.Copy(nextFight);
                newFightInstance.LoadMyTeam();
                newFightInstance.Team1GroupSet = GangbangInfo.CopyGroupSets(FightScene.FightScene.team1GroupSet);
                newFightInstance.ConvertTeamToGangbang();
                UILayerLoader.Remove<ArenaFightOver>();
                FightLoad.Go(newFightInstance, true);
            });
        }
    }
}
