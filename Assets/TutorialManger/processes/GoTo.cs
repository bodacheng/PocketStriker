using DummyLayerSystem;
using mainMenu;

public class GoTo : TutorialProcess
{
    private FrontLayer _frontLayer;
    private UpperInfoBar _upperInfoBar;

    private readonly string _goto;
    public GoTo(string step)
    {
        _goto = step;
    }
    
    public override void LocalUpdate()
    {
        if (_frontLayer == null)
        {
            _frontLayer = UILayerLoader.Get<FrontLayer>();
            if (_frontLayer != null)
            {
                _frontLayer.PlsClickBtn(_goto);
            }
        }
        
        if (_upperInfoBar == null)
        {
            _upperInfoBar = UILayerLoader.Get<UpperInfoBar>();
            if (_upperInfoBar != null)
            {
                _upperInfoBar.Interactable(false);
            }
        }
    }
    
    public override bool CanEnterOtherProcess()
    {
        switch (_goto)
        {
            case "arcade":
                return ProcessesRunner.Main.currentProcess.Step == MainSceneStep.QuestInfo;
            case "arena":
                return ProcessesRunner.Main.currentProcess.Step == MainSceneStep.Arena;
            case "unit":
                return ProcessesRunner.Main.currentProcess.Step == MainSceneStep.UnitList;
            case "train":
                return ProcessesRunner.Main.currentProcess.Step == MainSceneStep.SelfFightFront;
            case "stones":
                return ProcessesRunner.Main.currentProcess.Step == MainSceneStep.SkillStoneList;
            case "gotcha":
                return ProcessesRunner.Main.currentProcess.Step == MainSceneStep.GotchaFront;
        }

        return true;
    }
}