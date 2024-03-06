using mainMenu;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using dataAccess;
using DummyLayerSystem;

public class SkillEditPage : MSceneProcess
{
    private SkillEditLayer layer;
    
    void ItemsLoadFinished(bool value)
    {
        missionWatcher.Finish("itemsLoadFinished", value);
    }
    
    async UniTask EnterProcess()
    {
        ProgressLayer.Loading(string.Empty);
        await Stones.RenderAll();
        ProgressLayer.Close();
        layer = UILayerLoader.Load<SkillEditLayer>();
        await layer.Setup((x) =>
        {
            x.CamConnector.ShowMyModel(PreScene.target.Focusing != null ? PreScene.target.Focusing.id : null).Forget();
        });
        LowerMainBar.Open();
        ReturnLayer.MoveFront();
        SetLoaded(true);
    }
    
    public SkillEditPage()
    {
        Step = MainSceneStep.UnitSkillEdit;
    }
    
    public override void ProcessEnter()
    {
        PlayFabReadClient.LoadItems(ItemsLoadFinished);
        missionWatcher = new MissionWatcher(
            new List<string>() {"itemsLoadFinished"},
            ()=>EnterProcess().Forget()
        );
    }
    
    public override void ProcessEnd()
    {
        HurtObjectManager.Clear();
        EffectsManager.Clear();
        UILayerLoader.Remove<SkillEditLayer>();
    }
}
